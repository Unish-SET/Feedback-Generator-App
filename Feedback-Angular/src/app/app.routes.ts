import { Routes } from '@angular/router';
import { authGuard, adminGuard, publicGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'dashboard',
    pathMatch: 'full'
  },
  {
    path: 'auth',
    canActivate: [publicGuard],
    loadChildren: () => import('./features/auth/auth.routes').then(m => m.AUTH_ROUTES)
  },
  {
    path: 'survey/:publicToken/respond',
    loadComponent: () => import('./features/public-survey/public-survey.component').then(m => m.PublicSurveyComponent)
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./shared/components/shell/shell.component').then(m => m.ShellComponent),
    children: [
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'surveys',
        loadComponent: () => import('./features/surveys/list/survey-list.component').then(m => m.SurveyListComponent)
      },
      {
        path: 'surveys/new',
        loadComponent: () => import('./features/surveys/detail/survey-detail.component').then(m => m.SurveyDetailComponent)
      },
      {
        path: 'surveys/:id/edit',
        loadComponent: () => import('./features/surveys/detail/survey-detail.component').then(m => m.SurveyDetailComponent)
      },
      {
        path: 'surveys/:id/builder',
        loadComponent: () => import('./features/surveys/builder/survey-builder.component').then(m => m.SurveyBuilderComponent)
      },
      {
        path: 'surveys/:id/responses',
        loadComponent: () => import('./features/responses/responses.component').then(m => m.ResponsesComponent)
      },
      {
        path: 'surveys/:id/analytics',
        loadComponent: () => import('./features/analytics/analytics.component').then(m => m.AnalyticsComponent)
      },
      {
        path: 'question-bank',
        loadComponent: () => import('./features/question-bank/question-bank.component').then(m => m.QuestionBankComponent)
      },
      {
        path: 'admin',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/admin/admin.component').then(m => m.AdminComponent)
      }
    ]
  },
  {
    path: '**',
    loadComponent: () => import('./shared/components/not-found/not-found.component').then(m => m.NotFoundComponent)
  }
];
