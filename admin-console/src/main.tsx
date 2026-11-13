import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MotionConfig } from 'motion/react'
import { lazy, StrictMode, Suspense } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider, Guard } from '@/auth'
import { ToastProvider } from '@/components/toast'
import { Shell } from '@/layout/Shell'
import './index.css'

// Pages chargées à la demande : premier affichage plus léger (graphiques du tableau de bord à part).
const AccountPage = lazy(() => import('@/pages/Account').then((m) => ({ default: m.AccountPage })))
const AdminsPage = lazy(() => import('@/pages/Admins').then((m) => ({ default: m.AdminsPage })))
const BackupsPage = lazy(() => import('@/pages/Backups').then((m) => ({ default: m.BackupsPage })))
const MemberDetailPage = lazy(() => import('@/pages/MemberDetail').then((m) => ({ default: m.MemberDetailPage })))
const AuditPage = lazy(() => import('@/pages/Audit').then((m) => ({ default: m.AuditPage })))
const CommunitiesPage = lazy(() => import('@/pages/Communities').then((m) => ({ default: m.CommunitiesPage })))
const DashboardPage = lazy(() => import('@/pages/Dashboard').then((m) => ({ default: m.DashboardPage })))
const EventsPage = lazy(() => import('@/pages/Events').then((m) => ({ default: m.EventsPage })))
const EditorialPage = lazy(() => import('@/pages/Editorial').then((m) => ({ default: m.EditorialPage })))
const JobsPage = lazy(() => import('@/pages/Jobs').then((m) => ({ default: m.JobsPage })))
const JobOffersPage = lazy(() => import('@/pages/JobOffers').then((m) => ({ default: m.JobOffersPage })))
const JobsPreviewPage = lazy(() => import('@/pages/JobsPreview').then((m) => ({ default: m.JobsPreviewPage })))
const ExportsPage = lazy(() => import('@/pages/Exports').then((m) => ({ default: m.ExportsPage })))
const ForbiddenPage = lazy(() => import('@/pages/Errors').then((m) => ({ default: m.ForbiddenPage })))
const NotFoundPage = lazy(() => import('@/pages/Errors').then((m) => ({ default: m.NotFoundPage })))
const LoginPage = lazy(() => import('@/pages/Login').then((m) => ({ default: m.LoginPage })))
const MembersPage = lazy(() => import('@/pages/Members').then((m) => ({ default: m.MembersPage })))
const PostsPage = lazy(() => import('@/pages/Posts').then((m) => ({ default: m.PostsPage })))
const ReportsPage = lazy(() => import('@/pages/Reports').then((m) => ({ default: m.ReportsPage })))
const SystemPage = lazy(() => import('@/pages/System').then((m) => ({ default: m.SystemPage })))

const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, refetchOnWindowFocus: false, staleTime: 15_000 } } })

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <MotionConfig reducedMotion="user">
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <AuthProvider>
          <BrowserRouter>
            <Suspense fallback={<div className="grid h-full place-items-center text-muted">Chargement…</div>}>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route element={<Guard><Shell /></Guard>}>
                <Route index element={<Guard permission="DashboardView"><DashboardPage /></Guard>} />
                <Route path="reports" element={<Guard permission="ReportsManage"><ReportsPage /></Guard>} />
                <Route path="members" element={<Guard permission="MembersView"><MembersPage /></Guard>} />
                <Route path="members/:id" element={<Guard permission="MembersView"><MemberDetailPage /></Guard>} />
                <Route path="posts" element={<Guard permission="ContentModerate"><PostsPage /></Guard>} />
                <Route path="communities" element={<Guard permission="CommunitiesManage"><CommunitiesPage /></Guard>} />
                <Route path="events" element={<Guard permission="EventsManage"><EventsPage /></Guard>} />
                <Route path="editorial" element={<Guard permission="EditorialManage"><EditorialPage /></Guard>} />
                <Route path="jobs" element={<Guard permission="JobsManage"><JobsPage /></Guard>} />
                <Route path="jobs/offers" element={<Guard permission="JobsManage"><JobOffersPage /></Guard>} />
                <Route path="jobs/preview" element={<Guard permission="JobsManage"><JobsPreviewPage /></Guard>} />
                <Route path="exports" element={<Guard permission="ExportsDownload"><ExportsPage /></Guard>} />
                <Route path="admins" element={<Guard permission="AdminsManage"><AdminsPage /></Guard>} />
                <Route path="audit" element={<Guard permission="AuditView"><AuditPage /></Guard>} />
                <Route path="system" element={<Guard permission="SystemView"><SystemPage /></Guard>} />
                <Route path="backups" element={<Guard permission="BackupsManage"><BackupsPage /></Guard>} />
                <Route path="account" element={<AccountPage />} />
                <Route path="forbidden" element={<ForbiddenPage />} />
                <Route path="*" element={<NotFoundPage />} />
              </Route>
              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
            </Suspense>
          </BrowserRouter>
        </AuthProvider>
      </ToastProvider>
    </QueryClientProvider>
    </MotionConfig>
  </StrictMode>,
)
