// Contrat JSON de /api/admin (mêmes noms que les DTO C#, dates ISO).

export type AdminRole = 'SuperAdmin' | 'Admin' | 'Moderator' | 'Accountant' | 'Developer'

export type Permission =
  | 'DashboardView' | 'MembersView' | 'MembersSuspend' | 'ContentModerate' | 'ReportsManage' | 'CommunitiesManage'
  | 'EventsManage' | 'ExportsDownload' | 'AdminsManage' | 'AuditView' | 'SystemView' | 'SystemReset' | 'BackupsManage'

export interface AdminMe {
  id: string
  email: string
  displayName: string
  role: AdminRole
  permissions: Permission[]
}

export interface Paged<T> {
  items: T[]
  totalCount: number
  skip: number
}

export interface DailyPoint { day: string; count: number }
export interface LabelCount { label: string; count: number }

export interface AdminStats {
  members: number
  newMembers7Days: number
  newMembers30Days: number
  verifiedMembers: number
  suspendedMembers: number
  posts: number
  hiddenPosts: number
  comments: number
  communities: number
  privateCommunities: number
  upcomingEvents: number
  eventParticipations: number
  messages: number
  openReports: number
  signups: DailyPoint[]
  postsPerDay: DailyPoint[]
  topCommunities: LabelCount[]
  membersByCountry: LabelCount[]
}

export interface AdminMember {
  id: string
  firstName: string
  lastName: string
  email: string
  country: string
  city: string
  avatarColor: string
  avatarImage: string | null
  joinedAt: string
  isEmailVerified: boolean
  isSuspended: boolean
  suspensionReason: string | null
  postCount: number
  commentCount: number
  reportCount: number
}

export type PostVisibility = 'Public' | 'CommunityMembers' | 'OnlyMe'

export interface AdminPost {
  id: string
  authorId: string
  authorName: string
  communityId: string | null
  communityName: string | null
  content: string
  imagePath: string | null
  createdAt: string
  visibility: PostVisibility
  likeCount: number
  commentCount: number
  isHidden: boolean
  hiddenReason: string | null
  reportCount: number
}

export interface MemberCommunity {
  id: string
  name: string
  role: 'Member' | 'Moderator' | 'Owner'
  isPrivate: boolean
  joinedAt: string
}

export interface MemberEvent {
  id: string
  title: string
  city: string
  startsAt: string
  isOrganizer: boolean
  participants: number
}

export interface MemberReport {
  id: string
  reporterName: string
  reason: ReportReason
  status: ReportStatus
  details: string
  createdAt: string
}

export interface MemberDevice {
  platform: string
  registeredAt: string
  lastSeenAt: string
}

export interface AdminMemberDetail {
  member: AdminMember
  profession: string
  bio: string
  communities: string[]
  recentPosts: AdminPost[]
  originCity: string
  interests: string[] | null
  coverImage: string | null
  suspendedAt: string | null
  memberships: MemberCommunity[] | null
  events: MemberEvent[] | null
  messaging: { conversations: number; messagesSent: number; lastMessageAt: string | null } | null
  reportsAbout: MemberReport[] | null
  reportsFiled: number
  blockedCount: number
  blockedByCount: number
  devices: MemberDevice[] | null
}

export type BackupKind = 'Manual' | 'Daily' | 'Weekly' | 'Monthly' | 'Media'

export interface BackupFile {
  kind: BackupKind
  name: string
  size: number
  createdAt: string
  canDelete: boolean
  offsite: boolean
}

export interface RemoteBackup {
  kind: BackupKind
  name: string
  size: number
  uploadedAt: string
  isLocal: boolean
}

export interface OffsiteStatus {
  enabled: boolean
  bucket: string | null
  retentionDays: number
  intervalMinutes: number
  lastSyncAt: string | null
  lastError: string | null
  remoteBytes: number
  remote: RemoteBackup[]
}

export interface BackupOverview {
  directoryAvailable: boolean
  canCreate: boolean
  pgDumpVersion: string | null
  lastDatabaseBackupAt: string | null
  totalBytes: number
  freeBytes: number | null
  files: BackupFile[]
  offsite: OffsiteStatus | null
}

export interface AdminComment {
  id: string
  postId: string
  authorId: string
  authorName: string
  content: string
  createdAt: string
}

export interface AdminCommunity {
  id: string
  name: string
  category: string
  city: string
  country: string
  visibility: 'Public' | 'Private'
  ownerName: string
  memberCount: number
  postCount: number
  pendingRequests: number
  createdAt: string
}

export interface AdminEvent {
  id: string
  title: string
  category: string
  city: string
  startsAt: string
  organizerName: string
  communityName: string | null
  participants: number
  capacity: number | null
}

export type ReportTarget = 'Post' | 'Comment' | 'User' | 'Community'
export type ReportReason = 'Spam' | 'Harassment' | 'Inappropriate' | 'FalseInformation' | 'Other'
export type ReportStatus = 'Open' | 'Resolved' | 'Dismissed'
export type ReportDecision = 'dismiss' | 'hide' | 'delete' | 'suspend'

export interface AdminReport {
  id: string
  targetType: ReportTarget
  targetId: string
  targetPreview: string
  targetAuthorName: string | null
  postId: string | null
  targetExists: boolean
  reason: ReportReason
  details: string
  reporterName: string
  status: ReportStatus
  createdAt: string
  closedAt: string | null
  resolution: string
  sameTargetOpenCount: number
}

export interface AdminAccount {
  id: string
  email: string
  displayName: string
  role: AdminRole
  isActive: boolean
  createdAt: string
  lastLoginAt: string | null
}

export interface AuditEntry {
  id: string
  adminName: string
  action: string
  targetType: string
  targetId: string | null
  details: string
  at: string
}

export interface SystemInfo {
  version: string
  environment: string
  runtime: string
  startedAt: string
  uptime: string
  databaseOnline: boolean
  database: string
  mediaFiles: number
  mediaBytes: number
  tables: LabelCount[]
  canResetDemo: boolean
}
