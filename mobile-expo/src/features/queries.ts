import { type InfiniteData, type QueryClient, useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Share } from 'react-native';
import { SITE_URL } from '@/core/config';
import { useSession } from '@/core/session';
import * as fmt from '@/core/format';
import { toast } from '@/core/toast';
import { data } from '@/data';
import type {
  AppNotification, Comment, ConversationSummary, Message,
  Community, CommunityEvent, CreateCommunityRequest, CreateEventRequest, CreatePostRequest, EventFilter, NotificationCategory,
  OutgoingFile, Paged, Post, PostFilter, UpdatePostRequest, UpdateProfileRequest, JobFilter, JobOffer, JobPage,
  DocumentKind, OfferingInput, ProviderDetail, ProviderInput, ProviderPage, ServiceReportTarget, ServiceRequestItem, ServiceSearch,
} from '@/data/types';
import { errorMessage, toggleReaction } from '@/data/types';

// Couche « cas d'usage » de l'interface : lectures (useQuery) et écritures (useMutation) avec mise à jour des caches,
// pour que tous les écrans restent synchronisés (équivalent du messenger de la version Avalonia).

export const PAGE_SIZE = 10;
const onError = (e: unknown) => toast(errorMessage(e), 'error');

// --- Membres ----------------------------------------------------------------------------

export const useUser = (id: string | undefined) =>
  useQuery({ queryKey: ['user', id], queryFn: () => data().getUser(id!), enabled: !!id });

/** Résolution groupée des auteurs (une requête par liste, pas par carte). */
export function useUsersMap(ids: string[]) {
  const key = [...new Set(ids)].sort();
  const q = useQuery({ queryKey: ['users-batch', key], queryFn: () => data().getUsers(key), enabled: key.length > 0, staleTime: 60_000 });
  return new Map((q.data ?? []).map((u) => [u.id, u]));
}

export const useSearchUsers = (search: string) =>
  useQuery({ queryKey: ['users', search], queryFn: () => data().searchUsers(search) });

export function useUpdateProfile() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (r: UpdateProfileRequest) => data().updateProfile(r),
    onSuccess: (user) => {
      useSession.getState().update(user);
      qc.setQueryData(['user', user.id], user);
      qc.invalidateQueries({ queryKey: ['users-batch'] });
    },
    onError,
  });
}

// --- Publications -----------------------------------------------------------------------

export const useFeed = () =>
  useInfiniteQuery({
    queryKey: ['feed'],
    queryFn: ({ pageParam }) => data().getFeed(pageParam, PAGE_SIZE),
    initialPageParam: 0,
    getNextPageParam: (last: Paged<Post>) => (last.skip + last.items.length < last.totalCount ? last.skip + last.items.length : undefined),
  });

export const usePost = (id: string, enabled = true) => useQuery({ queryKey: ['post', id], queryFn: () => data().getPost(id), enabled });

export const usePosts = (filter: PostFilter, enabled = true) =>
  useQuery({ queryKey: ['posts', filter], queryFn: () => data().getPosts(filter), enabled });

export const useComments = (postId: string) =>
  useQuery({ queryKey: ['comments', postId], queryFn: () => data().getComments(postId) });

/** Remplace une publication dans tous les caches où elle apparaît. */
export function patchPost(qc: QueryClient, post: Post) {
  qc.setQueryData(['post', post.id], post);
  qc.setQueriesData<InfiniteData<Paged<Post>>>({ queryKey: ['feed'] }, (old) =>
    old && { ...old, pages: old.pages.map((p) => ({ ...p, items: p.items.map((x) => (x.id === post.id ? post : x)) })) });
  qc.setQueriesData<Post[]>({ queryKey: ['posts'] }, (old) => old?.map((x) => (x.id === post.id ? post : x)));
  qc.setQueryData<Post[]>(['savedPosts'], (old) => old?.map((x) => (x.id === post.id ? post : x)));
}

// --- Enregistrements et épingles ---------------------------------------------------------

export const useSavedIds = () =>
  useQuery({ queryKey: ['savedIds'], queryFn: () => data().getSavedPostIds(), staleTime: 60_000, select: (ids) => new Set(ids) });
export const useSavedPosts = (enabled = true) => useQuery({ queryKey: ['savedPosts'], queryFn: () => data().getSavedPosts(), enabled });

/** Enregistrer / retirer (mise à jour immédiate, annulée en cas d'erreur). */
export function useToggleSave() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (post: Post) => data().toggleSave(post.id),
    onMutate: (post) => {
      const previous = qc.getQueryData<string[]>(['savedIds']);
      qc.setQueryData<string[]>(['savedIds'], (old = []) => (old.includes(post.id) ? old.filter((x) => x !== post.id) : [post.id, ...old]));
      return previous;
    },
    onSuccess: (saved) => {
      qc.invalidateQueries({ queryKey: ['savedPosts'] });
      toast(saved ? 'Publication enregistrée.' : 'Retirée de vos enregistrements.');
    },
    onError: (e, _post, previous) => {
      qc.setQueryData(['savedIds'], previous);
      onError(e);
    },
  });
}

export function usePinPost() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ post, pinned }: { post: Post; pinned: boolean }) => data().pinPost(post.id, pinned),
    onSuccess: (post) => {
      patchPost(qc, post);
      qc.invalidateQueries({ queryKey: ['posts'] });
      toast(post.pinnedAt ? 'Publication épinglée sur votre profil.' : 'Publication désépinglée.', 'success');
    },
    onError,
  });
}

export function usePinComment(postId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (commentId: string | null) => data().pinComment(postId, commentId),
    onSuccess: (post) => patchPost(qc, post),
    onError,
  });
}

export function useToggleLike() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (post: Post) => data().toggleLike(post.id),
    onMutate: (post) => {
      const me = useSession.getState().user!.id;
      const liked = post.likedBy.includes(me);
      patchPost(qc, { ...post, likedBy: liked ? post.likedBy.filter((x) => x !== me) : [...post.likedBy, me] });
      return post;
    },
    onSuccess: (post) => patchPost(qc, post),
    onError: (e, _post, previous) => {
      if (previous) patchPost(qc, previous);
      onError(e);
    },
  });
}

/** Feuille de partage du téléphone ; le compteur n'augmente que si le partage a bien eu lieu. */
export function useSharePost() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ post, authorName }: { post: Post; authorName: string }) => {
      const excerpt = post.content.length > 200 ? `${post.content.slice(0, 200).trimEnd()}…` : post.content;
      const result = await Share.share({
        message: `${authorName} sur BéninConnect :\n\n« ${excerpt} »\n\n${SITE_URL}/post/${post.id}`,
      });
      return result.action === Share.sharedAction ? data().sharePost(post.id) : null;
    },
    onSuccess: (post) => {
      if (post) patchPost(qc, post);
    },
    onError,
  });
}

/** Retire une publication de tous les caches où elle apparaît. */
export function removePost(qc: QueryClient, id: string) {
  qc.removeQueries({ queryKey: ['post', id] });
  qc.setQueriesData<InfiniteData<Paged<Post>>>({ queryKey: ['feed'] }, (old) =>
    old && { ...old, pages: old.pages.map((p) => ({ ...p, items: p.items.filter((x) => x.id !== id) })) });
  qc.setQueriesData<Post[]>({ queryKey: ['posts'] }, (old) => old?.filter((x) => x.id !== id));
}

export function useDeletePost() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => data().deletePost(id),
    onSuccess: (_, id) => {
      removePost(qc, id);
      toast('Publication supprimée.', 'success');
    },
    onError,
  });
}

export function useCreatePost() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (r: CreatePostRequest) => data().createPost(r),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['feed'] });
      qc.invalidateQueries({ queryKey: ['posts'] });
      toast('Votre publication est en ligne.', 'success');
    },
  });
}

export function useUpdatePost() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdatePostRequest }) => data().updatePost(id, request),
    onSuccess: (post) => {
      patchPost(qc, post);
      toast('Publication modifiée.', 'success');
    },
  });
}

export function useAddComment(postId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ content, parentId, mentions }: { content: string; parentId?: string | null; mentions?: string[] }) =>
      data().addComment(postId, content, { parentId, mentions }),
    onSuccess: async () => {
      await qc.invalidateQueries({ queryKey: ['comments', postId] });
      patchPost(qc, await data().getPost(postId));
    },
  });
}

/** Réaction à un commentaire (mise à jour immédiate, annulée en cas d'erreur). */
export function useReactToComment(postId: string) {
  const qc = useQueryClient();
  const key = ['comments', postId];
  return useMutation({
    mutationFn: ({ commentId, emoji }: { commentId: string; emoji: string }) => data().reactToComment(postId, commentId, emoji),
    onMutate: ({ commentId, emoji }) => {
      const previous = qc.getQueryData<Comment[]>(key);
      const me = useSession.getState().user!.id;
      qc.setQueryData<Comment[]>(key, (old) => old?.map((c) => (c.id === commentId ? { ...c, reactions: toggleReaction(c.reactions, me, emoji) } : c)));
      return previous;
    },
    onSuccess: (comment) => qc.setQueryData<Comment[]>(key, (old) => old?.map((c) => (c.id === comment.id ? comment : c))),
    onError: (e, _v, previous) => {
      qc.setQueryData(key, previous);
      onError(e);
    },
  });
}

// --- Communautés ------------------------------------------------------------------------

export const useCommunities = () => useQuery({ queryKey: ['communities'], queryFn: () => data().getCommunities() });
export const useCommunity = (id: string) => useQuery({ queryKey: ['community', id], queryFn: () => data().getCommunity(id), enabled: !!id });
export const useJoinRequests = () => useQuery({ queryKey: ['joinRequests'], queryFn: () => data().getMyJoinRequests() });
export const useCommunityCategories = () => useQuery({ queryKey: ['communityCategories'], queryFn: () => data().getCommunityCategories(), staleTime: Infinity });
export const useMemberCommunities = (userId: string | undefined) =>
  useQuery({ queryKey: ['memberCommunities', userId], queryFn: () => data().getMemberCommunities(userId!), enabled: !!userId });
export const useMembers = (id: string, enabled = true) =>
  useQuery({ queryKey: ['members', id], queryFn: () => data().getMembers(id), enabled });

export function useCommunitiesMap(ids: (string | null)[]) {
  const key = [...new Set(ids.filter((x): x is string => !!x))].sort();
  const q = useQuery({ queryKey: ['communities-batch', key], queryFn: () => data().getCommunitiesByIds(key), enabled: key.length > 0, staleTime: 60_000 });
  return new Map((q.data ?? []).map((c) => [c.id, c]));
}

const refreshCommunities = (qc: QueryClient) =>
  Promise.all(['communities', 'community', 'joinRequests', 'members', 'memberCommunities', 'communities-batch', 'feed', 'posts', 'notifications', 'unreadNotifications']
    .map((k) => qc.invalidateQueries({ queryKey: [k] })));

export function useMembership() {
  const qc = useQueryClient();
  const run = (fn: () => Promise<string>, success: (status: string) => string) =>
    fn().then(async (status) => {
      await refreshCommunities(qc);
      toast(success(status), 'success');
    }).catch(onError);

  return {
    join: (c: Community) => run(() => data().joinCommunity(c.id), (s) => (s === 'Pending'
      ? 'Demande envoyée aux administrateurs de la communauté.'
      : `Vous avez rejoint « ${c.name} ».`)),
    leave: (c: Community) => run(() => data().leaveCommunity(c.id), () => `Vous avez quitté « ${c.name} ».`),
    cancel: (c: Community) => run(() => data().cancelJoinRequest(c.id), () => "Demande d'adhésion annulée."),
  };
}

export function useCreateCommunity() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (r: CreateCommunityRequest) => data().createCommunity(r),
    onSuccess: (c) => {
      refreshCommunities(qc);
      toast(`Communauté « ${c.name} » créée.`, 'success');
    },
  });
}

// --- Messagerie -------------------------------------------------------------------------

export const useConversations = () => useQuery({ queryKey: ['conversations'], queryFn: () => data().getConversations() });
/** live : présence de l'autre membre rafraîchie toutes les 30 s (écran de discussion). */
export const useConversation = (id: string, live = false) =>
  useQuery({ queryKey: ['conversation', id], queryFn: () => data().getConversation(id), refetchInterval: live ? 30_000 : false });
export const useMessages = (id: string) => useQuery({ queryKey: ['messages', id], queryFn: () => data().getMessages(id) });
export const useUnreadMessages = () =>
  useQuery({ queryKey: ['unreadMessages'], queryFn: () => data().getUnreadMessageCount(), refetchInterval: 30_000 });

/** Ajoute ou remplace un message dans le cache de sa conversation (envoi, temps réel). */
export function upsertMessage(qc: QueryClient, message: Message) {
  qc.setQueryData<Message[]>(['messages', message.conversationId], (old) => {
    if (!old) return old;
    return old.some((m) => m.id === message.id) ? old.map((m) => (m.id === message.id ? message : m)) : [...old, message];
  });
}

const refreshMessaging = (qc: QueryClient, id?: string) =>
  Promise.all([
    qc.invalidateQueries({ queryKey: ['conversations'] }),
    qc.invalidateQueries({ queryKey: ['unreadMessages'] }),
    ...(id ? [qc.invalidateQueries({ queryKey: ['messages', id] })] : []),
  ]);

export function useSendMessage(conversationId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ content, simulated }: { content: string; simulated?: boolean }) =>
      simulated ? data().simulateReply(conversationId, content) : data().sendMessage(conversationId, content),
    onSuccess: (message) => {
      upsertMessage(qc, message);
      return refreshMessaging(qc);
    },
    onError,
  });
}

/** Envoie une publication à un membre (ouvre ou crée la discussion). */
export function useSendPostTo() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async ({ userId, postId, content }: { userId: string; postId: string; content?: string }) => {
      const conversation = await data().startConversation(userId);
      return data().sendPost(conversation.id, postId, content);
    },
    onSuccess: (message) => {
      upsertMessage(qc, message);
      return refreshMessaging(qc);
    },
    onError,
  });
}

export function useSendAttachment(conversationId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ file, caption }: { file: OutgoingFile; caption?: string }) => data().sendAttachment(conversationId, file, caption),
    onSuccess: (message) => {
      upsertMessage(qc, message);
      return refreshMessaging(qc);
    },
    onError,
  });
}

export function useEditMessage(conversationId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ messageId, content }: { messageId: string; content: string }) => data().editMessage(conversationId, messageId, content),
    onSuccess: (message) => {
      upsertMessage(qc, message);
      return refreshMessaging(qc);
    },
    onError,
  });
}

export function useDeleteMessage(conversationId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (messageId: string) => data().deleteMessage(conversationId, messageId),
    onSuccess: (message) => {
      upsertMessage(qc, message);
      return refreshMessaging(qc);
    },
    onError,
  });
}

/** Supprime la discussion de ma liste (l'autre membre garde son historique). */
export function useDeleteConversation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (conversationId: string) => data().deleteConversation(conversationId),
    onMutate: (conversationId) => {
      qc.setQueryData<ConversationSummary[]>(['conversations'], (old) => old?.filter((s) => s.conversation.id !== conversationId));
    },
    onSuccess: (_r, conversationId) => {
      qc.removeQueries({ queryKey: ['messages', conversationId] });
      toast('Discussion supprimée.');
    },
    onError,
    onSettled: () => refreshMessaging(qc),
  });
}

export function useReactToMessage(conversationId: string) {
  const qc = useQueryClient();
  const key = ['messages', conversationId];
  return useMutation({
    mutationFn: ({ messageId, emoji }: { messageId: string; emoji: string }) => data().reactToMessage(conversationId, messageId, emoji),
    onMutate: ({ messageId, emoji }) => {
      const previous = qc.getQueryData<Message[]>(key);
      const me = useSession.getState().user!.id;
      qc.setQueryData<Message[]>(key, (old) => old?.map((m) => (m.id === messageId ? { ...m, reactions: toggleReaction(m.reactions, me, emoji) } : m)));
      return previous;
    },
    onSuccess: (message) => qc.setQueryData<Message[]>(key, (old) => old?.map((m) => (m.id === message.id ? message : m))),
    onError: (e, _v, previous) => {
      qc.setQueryData(key, previous);
      onError(e);
    },
  });
}

export function useMarkConversationRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => data().markConversationRead(id),
    onSuccess: (count) => (count > 0 ? refreshMessaging(qc) : undefined),
  });
}

// --- Événements -------------------------------------------------------------------------

export const useEvents = (filter: EventFilter) => useQuery({ queryKey: ['events', filter], queryFn: () => data().getEvents(filter) });
export const useEvent = (id: string) => useQuery({ queryKey: ['event', id], queryFn: () => data().getEvent(id) });
export const useCommunityEvents = (id: string, enabled = true) =>
  useQuery({ queryKey: ['communityEvents', id], queryFn: () => data().getCommunityEvents(id), enabled });
export const useEventCategories = () => useQuery({ queryKey: ['eventCategories'], queryFn: () => data().getEventCategories(), staleTime: Infinity });
export const useEventCities = () => useQuery({ queryKey: ['eventCities'], queryFn: () => data().getEventCities() });

export function patchEvent(qc: QueryClient, event: CommunityEvent) {
  qc.setQueryData(['event', event.id], event);
  for (const key of ['events', 'communityEvents']) {
    qc.setQueriesData<CommunityEvent[]>({ queryKey: [key] }, (old) => old?.map((e) => (e.id === event.id ? event : e)));
  }
}

export function useToggleParticipation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => data().toggleParticipation(id),
    onSuccess: ({ isParticipating, event }) => {
      patchEvent(qc, event);
      toast(isParticipating ? `Inscription confirmée : ${event.title}` : 'Votre inscription a été annulée.', isParticipating ? 'success' : 'info');
    },
    onError,
  });
}

export function useCreateEvent() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (r: CreateEventRequest) => data().createEvent(r),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['events'] });
      qc.invalidateQueries({ queryKey: ['communityEvents'] });
      qc.invalidateQueries({ queryKey: ['eventCities'] });
      toast('Événement publié.', 'success');
    },
  });
}

// --- Notifications ----------------------------------------------------------------------

export const useNotifications = (category?: NotificationCategory) =>
  useQuery({ queryKey: ['notifications', category ?? 'all'], queryFn: () => data().getNotifications(category) });
export const useUnreadNotifications = () =>
  useQuery({ queryKey: ['unreadNotifications'], queryFn: () => data().getUnreadNotificationCount(), refetchInterval: 30_000 });

export function useNotificationActions() {
  const qc = useQueryClient();
  const refresh = () => Promise.all([qc.invalidateQueries({ queryKey: ['notifications'] }), qc.invalidateQueries({ queryKey: ['unreadNotifications'] })]);
  return {
    markRead: (id: string) => data().markNotificationRead(id).then(refresh).catch(onError),
    markAll: () => data().markAllNotificationsRead().then(async () => {
      await refresh();
      toast('Toutes les notifications sont lues.');
    }).catch(onError),
    remove: (id: string) => {
      // Retirée tout de suite de la liste, puis synchronisée
      qc.setQueriesData<AppNotification[]>({ queryKey: ['notifications'] }, (old) => old?.filter((n) => n.id !== id));
      return data().deleteNotification(id).catch(onError).finally(refresh);
    },
    clear: (category?: NotificationCategory) => data().clearNotifications(category).then(async (count) => {
      await refresh();
      toast(count > 0 ? `${fmt.plural(count, 'notification')} supprimée${count > 1 ? 's' : ''}.` : 'Aucune notification à supprimer.');
    }).catch(onError),
  };
}

// --- Emploi ------------------------------------------------------------------------------

export const useJobs = (filter: JobFilter) =>
  useInfiniteQuery({
    queryKey: ['jobs', filter],
    queryFn: ({ pageParam }) => data().searchJobs(filter, pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (last: JobPage) => last.nextCursor,
  });

export const useJob = (id: string) => useQuery({ queryKey: ['job', id], queryFn: () => data().getJob(id) });

export const useJobFilters = () => useQuery({ queryKey: ['job-filters'], queryFn: () => data().getJobFilters(), staleTime: 5 * 60_000 });

export const useFavoriteJobs = () => useQuery({ queryKey: ['job-favorites'], queryFn: () => data().getFavoriteJobs() });

/** Favori : mis à jour tout de suite dans les listes et le détail. */
export function useToggleJobFavorite() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, favorite }: { id: string; favorite: boolean }) => data().setJobFavorite(id, favorite),
    onMutate: ({ id, favorite }) => {
      qc.setQueryData<JobOffer>(['job', id], (old) => (old ? { ...old, isFavorite: favorite } : old));
      qc.setQueriesData<InfiniteData<JobPage>>({ queryKey: ['jobs'] }, (old) =>
        old && { ...old, pages: old.pages.map((p) => ({ ...p, items: p.items.map((j) => (j.id === id ? { ...j, isFavorite: favorite } : j)) })) });
    },
    onSuccess: (_, { favorite }) => {
      qc.invalidateQueries({ queryKey: ['job-favorites'] });
      toast(favorite ? 'Offre ajoutée à vos favoris.' : 'Offre retirée de vos favoris.', 'success');
    },
    onError: (e, { id, favorite }) => {
      qc.setQueryData<JobOffer>(['job', id], (old) => (old ? { ...old, isFavorite: !favorite } : old));
      qc.invalidateQueries({ queryKey: ['jobs'] });
      onError(e);
    },
  });
}

export function useReportJob() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) => data().reportJob(id, reason),
    onSuccess: (_, { id }) => {
      qc.setQueryData<JobOffer>(['job', id], (old) => (old ? { ...old, reportedByMe: true } : old));
      toast('Merci. Notre équipe va vérifier cette offre.', 'success');
    },
    onError,
  });
}

// --- Services entre membres ----------------------------------------------------------------

export const useServiceCategories = () =>
  useQuery({ queryKey: ['service-categories'], queryFn: () => data().getServiceCategories(), staleTime: 10 * 60_000 });

export const useServicesInfo = () => useQuery({ queryKey: ['services-info'], queryFn: () => data().getServicesInfo(), staleTime: 60 * 60_000 });

export const useServiceSearch = (filter: ServiceSearch, enabled = true) =>
  useInfiniteQuery({
    queryKey: ['service-search', filter],
    queryFn: ({ pageParam }) => data().searchServices(filter, pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (last: ProviderPage) => last.nextCursor,
    enabled,
  });

export const useProvider = (id: string) => useQuery({ queryKey: ['provider', id], queryFn: () => data().getProvider(id) });

export const useFavoriteProviders = () => useQuery({ queryKey: ['provider-favorites'], queryFn: () => data().getFavoriteProviders() });

export function useToggleProviderFavorite() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, favorite }: { id: string; favorite: boolean }) => data().setProviderFavorite(id, favorite),
    onMutate: ({ id, favorite }) => {
      qc.setQueryData<ProviderDetail>(['provider', id], (old) => (old ? { ...old, isFavorite: favorite } : old));
      qc.setQueriesData<InfiniteData<ProviderPage>>({ queryKey: ['service-search'] }, (old) =>
        old && { ...old, pages: old.pages.map((p) => ({ ...p, items: p.items.map((x) => (x.id === id ? { ...x, isFavorite: favorite } : x)) })) });
    },
    onSuccess: (_, { favorite }) => {
      qc.invalidateQueries({ queryKey: ['provider-favorites'] });
      toast(favorite ? 'Prestataire ajouté à vos favoris.' : 'Prestataire retiré de vos favoris.', 'success');
    },
    onError: (e, { id }) => {
      qc.invalidateQueries({ queryKey: ['provider', id] });
      qc.invalidateQueries({ queryKey: ['service-search'] });
      onError(e);
    },
  });
}

export const useMyProvider = () => useQuery({ queryKey: ['my-provider'], queryFn: () => data().getMyProvider() });

/** Après une modification de l'espace prestataire : profil, recherche et fiche publique rafraîchis. */
function refreshProvider(qc: QueryClient) {
  ['my-provider', 'service-search', 'provider', 'service-categories'].forEach((k) => qc.invalidateQueries({ queryKey: [k] }));
}

export function useSaveMyProvider() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ input, declared }: { input: ProviderInput; declared: boolean }) => data().saveMyProvider(input, declared),
    onSuccess: (p) => { qc.setQueryData(['my-provider'], p); refreshProvider(qc); },
    onError,
  });
}

export function useSubmitMyProvider() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => data().submitMyProvider(),
    onSuccess: (p) => {
      qc.setQueryData(['my-provider'], p);
      toast(p.status === 'Active' ? 'Votre profil est en ligne.' : 'Profil envoyé : notre équipe le vérifie, vous serez prévenu.', 'success');
    },
    onError,
  });
}

export function useSaveOffering() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, input }: { id: string | null; input: OfferingInput }) => data().saveOffering(id, input),
    onSuccess: (o) => {
      refreshProvider(qc);
      toast(o.hiddenReason === 'AlertKeyword' ? 'Offre enregistrée : elle sera vérifiée par notre équipe avant publication.' : 'Offre enregistrée.',
        o.hiddenReason === 'AlertKeyword' ? 'info' : 'success');
    },
    onError,
  });
}

export function useDeleteOffering() {
  const qc = useQueryClient();
  return useMutation({ mutationFn: (id: string) => data().deleteOffering(id), onSuccess: () => refreshProvider(qc), onError });
}

export function useUploadProviderDocument() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ kind, file }: { kind: DocumentKind; file: OutgoingFile }) => data().uploadProviderDocument(kind, file),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['my-provider'] }); toast('Justificatif envoyé : notre équipe le vérifie.', 'success'); },
    onError,
  });
}

export function useDeleteProviderDocument() {
  const qc = useQueryClient();
  return useMutation({ mutationFn: (id: string) => data().deleteProviderDocument(id), onSuccess: () => qc.invalidateQueries({ queryKey: ['my-provider'] }), onError });
}

export const useServiceRequests = (role: 'Client' | 'Provider') =>
  useQuery({ queryKey: ['service-requests', role], queryFn: () => data().getServiceRequests(role) });

export function useCreateServiceRequest() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (r: { offeringId: string; message: string; preferredDate: string | null; city: string | null }) =>
      data().createServiceRequest(r.offeringId, r.message, r.preferredDate, r.city),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['service-requests'] });
      qc.invalidateQueries({ queryKey: ['conversations'] });
      toast('Demande envoyée : retrouvez l’échange dans vos messages.', 'success');
    },
    onError,
  });
}

const RESPONSE_TOASTS = { accept: 'Demande acceptée.', decline: 'Demande refusée.', complete: 'Prestation marquée comme terminée.', cancel: 'Demande annulée.' };

export function useRespondServiceRequest() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, action, reason }: { id: string; action: 'accept' | 'decline' | 'complete' | 'cancel'; reason?: string }) =>
      data().respondServiceRequest(id, action, reason),
    onSuccess: (r, { action }) => {
      qc.setQueriesData<ServiceRequestItem[]>({ queryKey: ['service-requests'] }, (old) => old?.map((x) => (x.id === r.id ? r : x)));
      qc.invalidateQueries({ queryKey: ['provider', r.providerId] });
      toast(RESPONSE_TOASTS[action], 'success');
    },
    onError,
  });
}

export function useReviewService() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ requestId, reviewId, rating, comment }: { requestId?: string; reviewId?: string; rating: number; comment: string | null }) =>
      reviewId ? data().editServiceReview(reviewId, rating, comment) : data().reviewServiceRequest(requestId!, rating, comment),
    onSuccess: () => {
      ['service-requests', 'provider'].forEach((k) => qc.invalidateQueries({ queryKey: [k] }));
      toast('Merci pour votre avis !', 'success');
    },
    onError,
  });
}

export function useReplyServiceReview() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, reply }: { id: string; reply: string }) => data().replyServiceReview(id, reply),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['provider'] }); toast('Réponse publiée.', 'success'); },
    onError,
  });
}

export function useReportService() {
  return useMutation({
    mutationFn: ({ target, id, reason }: { target: ServiceReportTarget; id: string; reason: string }) => data().reportService(target, id, reason),
    onSuccess: () => toast('Merci. Notre équipe va examiner ce signalement.', 'success'),
    onError,
  });
}
