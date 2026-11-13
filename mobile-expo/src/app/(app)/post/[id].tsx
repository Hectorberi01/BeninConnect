import { useLocalSearchParams } from 'expo-router';
import { PostCard, usePostContext } from '@/components/cards';
import { Header, QueryView, Screen } from '@/components/ui';
import type { Post } from '@/data/types';
import { CommentsPanel } from '@/features/comments/CommentsPanel';
import { usePost } from '@/features/queries';

export default function PostDetails() {
  const { id, focus } = useLocalSearchParams<{ id: string; focus?: string }>();
  const query = usePost(id);
  return (
    <Screen>
      <Header title="Publication" />
      <QueryView query={query}>{(post) => <Details post={post} focus={focus} />}</QueryView>
    </Screen>
  );
}

function Details({ post, focus }: { post: Post; focus?: string }) {
  const ctx = usePostContext([post]);
  const card = (
    <PostCard post={post} author={ctx.users.get(post.authorId)} community={post.communityId ? ctx.communities.get(post.communityId) : undefined} detailed />
  );
  return <CommentsPanel post={post} header={card} focus={focus} />;
}
