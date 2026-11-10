import { HttpTransportType, type HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { useEffect, useState } from 'react';
import { create } from 'zustand';
import { AppState } from 'react-native';
import { queryClient } from '@/core/queryClient';
import { data } from '@/data';
import { ApiDataSource } from '@/data/api/apiDataSource';
import type { Message } from '@/data/types';
import { upsertMessage } from './queries';

/** « En train d'écrire… » : fin d'affichage par conversation (horodatage). */
export const useTyping = create<{ until: Record<string, number> }>(() => ({ until: {} }));

/** L'autre membre est en train d'écrire dans cette conversation. */
export function useIsTyping(conversationId: string) {
  const until = useTyping((s) => s.until[conversationId] ?? 0);
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const left = until - Date.now();
    if (left <= 0) return;
    const t = setTimeout(() => setNow(Date.now()), left + 50);
    return () => clearTimeout(t);
  }, [until]);
  return until > now;
}

let current: HubConnection | null = null;
const lastSent: Record<string, number> = {};

/** Signale la saisie à l'autre membre (au plus toutes les 3 s). */
export function sendTyping(conversationId: string) {
  const now = Date.now();
  if (!current || current.state !== HubConnectionState.Connected || now - (lastSent[conversationId] ?? 0) < 3_000) return;
  lastSent[conversationId] = now;
  current.invoke('Typing', conversationId).catch(() => undefined);
}

const stopTyping = (conversationId: string) =>
  useTyping.setState((s) => ({ until: { ...s.until, [conversationId]: 0 } }));

const refreshLists = () => {
  queryClient.invalidateQueries({ queryKey: ['conversations'] }).catch(() => undefined);
  queryClient.invalidateQueries({ queryKey: ['unreadMessages'] }).catch(() => undefined);
};

/**
 * Messagerie en temps réel (SignalR, /hubs/chat) tant que le membre est connecté :
 * nouveaux messages, modifications, suppressions, réactions et accusés de lecture.
 * Application fermée : les notifications push prennent le relais.
 */
export function useRealtime() {
  useEffect(() => {
    const source = data();
    if (!(source instanceof ApiDataSource)) return; // démo hors ligne : pas de serveur

    const connection: HubConnection = new HubConnectionBuilder()
      .withUrl(`${source.apiUrl}/hubs/chat`, {
        accessTokenFactory: () => source.currentToken ?? '',
        transport: HttpTransportType.WebSockets,
        skipNegotiation: true,
      })
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
      // Erreurs gérées ici (nouvel essai) : pas d'écran rouge en développement
      .configureLogging(LogLevel.None)
      .build();

    connection.on('typing', (conversationId: string) => {
      useTyping.setState((s) => ({ until: { ...s.until, [conversationId]: Date.now() + 4_000 } }));
    });
    connection.on('messageCreated', (message: Message) => {
      stopTyping(message.conversationId);
      upsertMessage(queryClient, message);
      refreshLists();
    });
    connection.on('messageUpdated', (message: Message) => {
      upsertMessage(queryClient, message);
      refreshLists();
    });
    connection.on('conversationRead', (conversationId: string, readerId: string) => {
      queryClient.setQueryData<Message[]>(['messages', conversationId], (old) =>
        old?.map((m) => (m.senderId !== readerId && !m.isRead ? { ...m, isRead: true } : m)));
      refreshLists();
    });
    // Messages manqués pendant la coupure
    connection.onreconnected(() => {
      queryClient.invalidateQueries({ queryKey: ['messages'] }).catch(() => undefined);
      refreshLists();
    });

    current = connection;
    let stopped = false;
    const start = () => {
      if (stopped || connection.state !== HubConnectionState.Disconnected) return;
      connection.start().catch((e: Error) => {
        if (__DEV__) console.warn('[temps réel] connexion impossible :', e.message); // nouvel essai au retour au premier plan
      });
    };
    start();

    // Retour au premier plan : reconnexion si le système a coupé la connexion
    const sub = AppState.addEventListener('change', (state) => {
      if (state !== 'active') return;
      if (connection.state === HubConnectionState.Disconnected) {
        start();
        queryClient.invalidateQueries({ queryKey: ['messages'] }).catch(() => undefined);
        refreshLists();
      }
    });

    return () => {
      stopped = true;
      current = null;
      sub.remove();
      connection.stop().catch(() => undefined);
    };
  }, []);
}
