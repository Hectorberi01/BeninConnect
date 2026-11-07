import Constants from 'expo-constants';
import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import { router } from 'expo-router';
import { useEffect } from 'react';
import { Platform } from 'react-native';
import { data } from '@/data';
import type { AppNotification, NotificationCategory } from '@/data/types';
import { notificationHref } from '@/features/rules';
import { usePreferences } from './preferences';
import { queryClient } from './queryClient';
import { beforeSignOut } from './session';

// Notification reçue app ouverte : bannière + son, comme en arrière-plan.
Notifications.setNotificationHandler({
  handleNotification: async () => ({ shouldShowBanner: true, shouldShowList: true, shouldPlaySound: true, shouldSetBadge: true }),
});

let currentToken: string | null = null;

beforeSignOut(() => unregisterFromPush());

/** Catégories coupées dans Paramètres › Notifications (le serveur ne les pousse pas). */
function mutedCategories(): NotificationCategory[] {
  const p = usePreferences.getState();
  return ([
    ['Social', p.notifySocial], ['Communities', p.notifyCommunities], ['Messages', p.notifyMessages], ['Events', p.notifyEvents],
  ] as const).filter(([, on]) => !on).map(([c]) => c);
}

/**
 * Demande l'autorisation et enregistre le téléphone auprès de l'API.
 * Rien sur le web, le simulateur, en mode démo, ou sans projet EAS (projectId).
 */
export async function registerForPush(): Promise<string | null> {
  if (Platform.OS === 'web' || !Device.isDevice || data().kind !== 'api') return null;

  if (Platform.OS === 'android') {
    await Notifications.setNotificationChannelAsync('default', {
      name: 'BéninConnect', importance: Notifications.AndroidImportance.HIGH, vibrationPattern: [0, 200, 120, 200],
    });
  }

  let { status } = await Notifications.getPermissionsAsync();
  if (status !== 'granted') status = (await Notifications.requestPermissionsAsync()).status;
  if (status !== 'granted') return null;

  const projectId = Constants.expoConfig?.extra?.eas?.projectId ?? Constants.easConfig?.projectId;
  if (!projectId) {
    console.warn('Notifications push : projectId EAS absent (lancer « eas init »).');
    return null;
  }

  const token = (await Notifications.getExpoPushTokenAsync({ projectId })).data;
  await data().registerPushDevice(token, Platform.OS === 'ios' ? 'ios' : 'android', mutedCategories());
  currentToken = token;
  return token;
}

/** Déconnexion : à appeler AVANT d'effacer le jeton de session (la route est authentifiée). */
export async function unregisterFromPush() {
  if (!currentToken) return;
  const token = currentToken;
  currentToken = null;
  await data().unregisterPushDevice(token).catch(() => undefined);
}

function openFromPush(response: Notifications.NotificationResponse) {
  const d = response.notification.request.content.data as Record<string, string | undefined>;
  if (d.notificationId) data().markNotificationRead(d.notificationId).catch(() => undefined);
  const href = notificationHref({ target: d.target ?? 'None', targetId: d.targetId ?? null } as AppNotification);
  router.push(href ?? '/notifications');
}

/** Zone connectée : enregistrement, réglages à jour côté serveur, ouverture au toucher. */
export function usePushNotifications() {
  useEffect(() => {
    registerForPush().catch((e) => console.warn('Notifications push indisponibles', e));

    // Notification reçue : liste et compteur rafraîchis
    const received = Notifications.addNotificationReceivedListener(() => {
      queryClient.invalidateQueries({ queryKey: ['notifications'] }).catch(() => undefined);
      queryClient.invalidateQueries({ queryKey: ['unreadNotifications'] }).catch(() => undefined);
    });
    const tapped = Notifications.addNotificationResponseReceivedListener(openFromPush);

    // App lancée en touchant une notification
    const last = Notifications.getLastNotificationResponse();
    if (last) openFromPush(last);

    // Catégories modifiées dans les réglages : préférences renvoyées au serveur
    const unsubscribe = usePreferences.subscribe((s, prev) => {
      const changed = s.notifySocial !== prev.notifySocial || s.notifyCommunities !== prev.notifyCommunities
        || s.notifyMessages !== prev.notifyMessages || s.notifyEvents !== prev.notifyEvents;
      if (changed && currentToken) registerForPush().catch(() => undefined);
    });

    return () => {
      received.remove();
      tapped.remove();
      unsubscribe();
    };
  }, []);
}
