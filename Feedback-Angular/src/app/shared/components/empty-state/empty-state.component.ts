import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

export type EmptyStateType = 'surveys' | 'responses' | 'analytics' | 'users' | 'questions';

const CONFIGS: Record<EmptyStateType, {
  title: string; subtitle: string; actionLabel?: string; actionRoute?: string;
}> = {
  surveys: {
    title: 'No surveys yet',
    subtitle: 'Create your first survey to start collecting feedback from your audience.',
    actionLabel: 'Create Survey',
    actionRoute: '/surveys/new'
  },
  responses: {
    title: 'No responses yet',
    subtitle: 'Once your survey is live and people start submitting, their responses will appear here.'
  },
  analytics: {
    title: 'No analytics data yet',
    subtitle: 'Analytics will populate here once your survey receives at least one response.'
  },
  users: {
    title: 'No users found',
    subtitle: 'Users who register on the platform will appear here.'
  },
  questions: {
    title: 'No questions added',
    subtitle: 'Add your first question below to start building your survey.'
  }
};

@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './empty-state.component.html'
})
export class EmptyStateComponent {
  readonly type = input.required<EmptyStateType>();
  config() { return CONFIGS[this.type()]; }
}
