// Contrat JSON de /api/admin (mêmes noms que les DTO C#, dates ISO).

export type AdminRole = 'SuperAdmin' | 'Admin' | 'Moderator' | 'Accountant' | 'Developer'

export type Permission =
  | 'DashboardView' | 'MembersView' | 'MembersSuspend' | 'ContentModerate' | 'ReportsManage' | 'CommunitiesManage'
  | 'EventsManage' | 'ExportsDownload' | 'AdminsManage' | 'AuditView' | 'SystemView' | 'SystemReset' | 'BackupsManage'
  | 'EditorialManage' | 'JobsManage'

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
  schedule: BackupSchedule | null
}

export interface BackupSchedule {
  enabled: boolean
  dailyAtUtc: string
  keepDays: number
  keepWeeks: number
  keepMonths: number
  lastRunAt: string | null
  lastError: string | null
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

// --- Compte officiel (module éditorial) ---

export type CandidateStatus = 'Collected' | 'Drafted' | 'Approved' | 'Published' | 'Rejected' | 'Skipped' | 'Failed'

export interface EditorialCandidate {
  id: string
  status: CandidateStatus
  isManual: boolean
  sourceName: string
  sourceUrl: string
  sourceTitle: string
  sourceExcerpt: string
  draftContent: string | null
  targetCommunityId: string | null
  targetCommunityName: string | null
  images: string[]
  collectedAt: string
  scheduledAt: string | null
  reviewedAt: string | null
  reason: string | null
  publishedPostId: string | null
  publishedAt: string | null
  publishAttempts: number
  lastError: string | null
}

export interface EditorialSource {
  id: string
  name: string
  url: string
  isEnabled: boolean
  createdAt: string
  lastFetchedAt: string | null
  lastError: string | null
  collectedCount: number
}

export interface EditorialOverview {
  automationEnabled: boolean
  llmConfigured: boolean
  llmModel: string
  officialAccountId: string
  officialName: string
  maxLength: number
  counts: LabelCount[]
  sources: EditorialSource[]
}

export interface PipelineReport {
  collected: number
  drafted: number
  skipped: number
  published: number
  failed: number
}

// --- Emploi (offres importées) ---

export type JobSource = 'FranceTravail' | 'ReliefWeb' | 'Adzuna' | 'Remotive'
export type SecretState = 'None' | 'Console' | 'Environment' | 'Unreadable'
export type JobOfferStatus = 'Active' | 'Expired' | 'Hidden'
export type HiddenReason = 'None' | 'AlertKeyword' | 'Reports' | 'Admin'
export type AdminOfferView = 'Review' | 'Hidden' | 'Reported' | 'All'

export interface JobSearch {
  key: string
  keywords?: string | null
  region?: string | null
  department?: string | null
  contractType?: string | null
  country?: string | null
  category?: string | null
  location?: string | null
}

export interface JobSourceConfig {
  source: JobSource
  label: string
  isEnabled: boolean
  importIntervalMinutes: number
  minIntervalMinutes: number
  clientId: string | null
  needsClientId: boolean
  needsSecret: boolean
  secretState: SecretState
  secretUpdatedAt: string | null
  searches: JobSearch[]
  lastRunAt: string | null
  lastSuccessAt: string | null
  lastError: string | null
  lastAdded: number
  lastUpdated: number
  lastDuplicates: number
  lastHidden: number
  lastExpired: number
  activeOffers: number
}

export interface JobsOverview {
  workerEnabled: boolean
  sources: JobSourceConfig[]
  alertKeywords: string[]
  reportThreshold: number
  counts: LabelCount[]
}

export interface JobImportReport {
  source: JobSource
  fetched: number
  added: number
  updated: number
  duplicates: number
  hidden: number
  expired: number
  errors: string[]
}

export interface AdminJobOffer {
  id: string
  title: string
  companyName: string | null
  locationLabel: string
  country: string
  source: JobSource
  sourceLabel: string
  status: JobOfferStatus
  hiddenReason: HiddenReason
  needsReview: boolean
  alertKeyword: string | null
  reportCount: number
  isDuplicate: boolean
  publishedAt: string
  importedAt: string
  applyUrl: string
  excerpt: string
  reports: { reason: string; createdAt: string; handled: boolean }[]
}
