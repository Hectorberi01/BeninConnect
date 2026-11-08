import Constants from 'expo-constants';
import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import { router, useFocusEffect } from 'expo-router';
import { useCallback, useEffect } from 'react';
import { Platform } from 'react-native';
import { data } from '@/data';
import type { AppNotification, NotificationCategory } from '@/data/types';
import { notificationHref } from '@/features/rules';
import { usePreferences } from './preferences';
import { queryClient } from './queryClient';
import { beforeSignOut } from './session';

/** Discussion affichée à l'écran (null sinon). */
let openConversationId: string | null = null;

/** À appeler dans l'écran de discussion. */
export function useOpenConversation(id: string) {
  useFocusEffect(useCallback(() => {
    openConversationId = id;
    return () => {
      if (openConversationId === id) openConversationId = null;
    };
  }, [id]));
}

function isOpenConversation(d: Record<string, unknown>) {
  return d.target === 'Conversation' && !!d.targetId && d.targetId === openConversationId;
}

// Notification reçue app ouverte : bannière + son, sauf pour la discussion déjà affichée.
Notifications.setNotificationHandler({
  handleNotification: async (n) => {
    const show = !isOpenConversation(n.request.content.data ?? {});
    return { shouldShowBanner: show, shouldShowList: show, shouldPlaySound: show, shouldSetBadge: true };
  },
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

/** Notifications déjà ouvertes : une seule ouverture par notification. */
const handled = new Set<string>();

function openFromPush(response: Notifications.NotificationResponse) {
  const key = response.notification.request.identifier;
  if (handled.has(key)) return;
  handled.add(key);

  const d = response.notification.request.content.data as Record<string, string | undefined>;
  if (d.notificationId) data().markNotificationRead(d.notificationId).catch(() => undefined);
  if (isOpenConversation(d)) return; // déjà sur cette discussion

  const href = notificationHref({ target: d.target ?? 'None', targetId: d.targetId ?? null } as AppNotification) ?? '/notifications';
  // Depuis une autre discussion : on la remplace, pour ne pas empiler les écrans.
  if (openConversationId && d.target === 'Conversation') router.replace(href);
  else router.push(href);
}

/** Zone connectée : enregistrement, réglages à jour côté serveur, ouverture au toucher. */
export function usePushNotifications() {
  useEffect(() => {
    // Pas de notifications push sur le web
    if (Platform.OS === 'web') return;
    registerForPush().catch((e) => console.warn('Notifications push indisponibles', e));

    // Notification reçue : liste et compteur rafraîchis
    const received = Notifications.addNotificationReceivedListener(() => {
      queryClient.invalidateQueries({ queryKey: ['notifications'] }).catch(() => undefined);
      queryClient.invalidateQueries({ queryKey: ['unreadNotifications'] }).catch(() => undefined);
    });
    const tapped = Notifications.addNotificationResponseReceivedListener(openFromPush);

    // App lancée en touchant une notification
    const last = Notifications.getLastNotificationResponse();
    if (last) {
      openFromPush(last);
      Notifications.clearLastNotificationResponse();
    }

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
