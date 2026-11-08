import { HttpTransportType, type HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { useEffect } from 'react';
import { AppState } from 'react-native';
import { queryClient } from '@/core/queryClient';
import { data } from '@/data';
import { ApiDataSource } from '@/data/api/apiDataSource';
import type { Message } from '@/data/types';
import { upsertMessage } from './queries';

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

    connection.on('messageCreated', (message: Message) => {
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
      sub.remove();
      connection.stop().catch(() => undefined);
    };
  }, []);
}
