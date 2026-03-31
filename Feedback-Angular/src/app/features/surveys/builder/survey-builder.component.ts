import { Component, inject, signal, OnInit, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpEventType, HttpResponse } from '@angular/common/http';
import { ReactiveFormsModule, FormBuilder, FormArray, Validators, FormGroup } from '@angular/forms';
import { RouterLink, Router } from '@angular/router';
import { SurveyService } from '../../../core/services/survey.service';
import { QuestionService } from '../../../core/services/question.service';
import { QuestionBankService } from '../../../core/services/question-bank.service';
import { ToastService } from '../../../core/services/toast.service';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { Survey, Question, SurveyListItem, BankQuestion, ApiResponse, QuestionImportResult } from '../../../shared/models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';

const QUESTION_TYPES = [
  { value: 'ShortText',      label: 'Short Text',      icon: '📝' },
  { value: 'LongText',       label: 'Long Text',       icon: '📄' },
  { value: 'SingleChoice',   label: 'Single Choice',   icon: '⚪' },
  { value: 'MultipleChoice', label: 'Multiple Choice', icon: '☑️' },
  { value: 'RatingScale',    label: 'Rating Scale',    icon: '⭐' }
];

@Component({
  selector: 'app-survey-builder',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, EmptyStateComponent],
  template: `
    <div class="max-w-3xl mx-auto space-y-4 pb-28 slide-up">
      <!-- Header -->
      <div class="flex items-center gap-3">
        <a routerLink="/surveys" class="btn-ghost btn-icon">
          <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M15 19l-7-7 7-7" />
          </svg>
        </a>
        <div>
          <h1 class="text-xl font-semibold text-surface-900">Survey Builder</h1>
          @if (survey()) {
            <p class="text-sm text-surface-500">{{ survey()!.title }}</p>
          }
        </div>
        <div class="ml-auto">
          @if (survey()?.state === 'Active') {
            <div class="flex items-center gap-2 px-3 py-1.5 rounded-xl text-xs font-medium"
              style="background:#f0fdf4; color:#16a34a; border:1px solid #bbf7d0">
              <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M5.636 18.364a9 9 0 010-12.728m12.728 0a9 9 0 010 12.728M12 12h.01"/>
              </svg>
              Live — pause to edit questions
            </div>
          }
          @if (survey()?.state === 'Closed') {
            <div class="flex items-center gap-2 px-3 py-1.5 rounded-xl text-xs font-medium"
              style="background:#fff7ed; color:#ea580c; border:1px solid #fed7aa">
              <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z"/>
              </svg>
              Closed — read only
            </div>
          }
          @if (isEditable()) {
            <div class="flex items-center gap-2 px-3 py-1.5 rounded-xl text-xs font-medium"
              style="background:#f0fdf4; color:#16a34a; border:1px solid #bbf7d0">
              <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M16.862 4.487l1.687-1.688a1.875 1.875 0 112.652 2.652L10.582 16.07a4.5 4.5 0 01-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 011.13-1.897l8.932-8.931z"/>
              </svg>
              Editing — questions unlocked
            </div>
          }
        </div>
      </div>

      @if (loading()) {
        <div class="space-y-4">
          @for (i of [1,2,3]; track i) { <div class="skeleton h-32 rounded-2xl"></div> }
        </div>
      } @else {
        @if (questions().length === 0) {
          <div class="card">
            <app-empty-state type="questions" />
          </div>
        }

        <!-- Questions List -->
        <div class="space-y-3">
          @for (q of questions(); track q.id; let qi = $index) {
            <div class="card p-5 group">
              @if (editingId() === q.id) {
                <form [formGroup]="editFormGroup" (ngSubmit)="saveEdit(q.id)" class="space-y-4">
                  <div class="grid grid-cols-2 gap-3">
                    <div class="col-span-2">
                      <label class="label text-xs">Question Text</label>
                      <input type="text" formControlName="text" class="input text-sm" placeholder="Enter question..." />
                    </div>
                    <div>
                      <label class="label text-xs">Type</label>
                      <select formControlName="type" class="input text-sm" (change)="onEditTypeChange()">
                        @for (t of questionTypes; track t.value) {
                          <option [value]="t.value">{{ t.icon }} {{ t.label }}</option>
                        }
                      </select>
                    </div>
                    <div class="flex items-end pb-1">
                      <label class="flex items-center gap-2 text-sm cursor-pointer">
                        <input type="checkbox" formControlName="isRequired" class="rounded text-brand-600" />
                        Required
                      </label>
                    </div>
                  </div>

                  @if (isChoiceType(editFormGroup.get('type')?.value)) {
                    <div>
                      <label class="label text-xs">Options</label>
                      <div formArrayName="options" class="space-y-2">
                        @for (opt of editOptions.controls; track $index; let oi = $index) {
                          <div class="flex gap-2" [formGroupName]="oi">
                            <input type="text" formControlName="text" class="input text-sm flex-1" [placeholder]="'Option ' + (oi + 1)" />
                            <button type="button" (click)="removeEditOption(oi)" class="btn-ghost btn-icon btn-sm text-red-400">×</button>
                          </div>
                        }
                      </div>
                      <button type="button" (click)="addEditOption()" class="btn-secondary btn-sm mt-2">+ Add Option</button>
                    </div>
                  }

                  <div class="flex gap-2">
                    <button type="submit" [disabled]="editFormGroup.invalid || saving()" class="btn-primary btn-sm">
                      @if (saving()) { <span class="inline-block w-3 h-3 border-2 border-white/30 border-t-white rounded-full animate-spin"></span> }
                      Save
                    </button>
                    <button type="button" (click)="cancelEdit()" class="btn-secondary btn-sm">Cancel</button>
                  </div>
                </form>
              } @else {
                <div class="flex items-start gap-3">
                  <div class="flex-1">
                    <div class="flex items-center gap-2 mb-1">
                      <span class="text-xs font-semibold text-surface-400">Q{{ qi + 1 }}</span>
                      <span class="badge badge-draft text-xs">{{ q.type }}</span>
                      @if (q.isRequired) {
                        <span class="text-red-400 text-xs font-medium">Required</span>
                      }
                    </div>
                    <p class="text-sm font-medium text-surface-900">{{ q.text }}</p>
                    @if (q.options && q.options.length > 0) {
                      <div class="mt-2 space-y-1">
                        @for (opt of q.options; track opt.id) {
                          <div class="flex items-center gap-2 text-xs text-surface-500">
                            <span class="w-3 h-3 rounded-full border border-surface-300 flex-shrink-0"></span>
                            {{ opt.text }}
                          </div>
                        }
                      </div>
                    }
                  </div>
                  @if (isEditable()) {
                    <div class="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                      <button (click)="startEdit(q)" class="btn-ghost btn-icon btn-sm text-brand-600" title="Edit" aria-label="Edit question">
                        <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M16.862 4.487l1.687-1.688a1.875 1.875 0 112.652 2.652L10.582 16.07a4.5 4.5 0 01-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 011.13-1.897l8.932-8.931zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0115.75 21H5.25A2.25 2.25 0 013 18.75V8.25A2.25 2.25 0 015.25 6H10"/></svg>
                      </button>
                      <button (click)="deleteQuestion(q)" class="btn-ghost btn-icon btn-sm text-red-500" title="Delete" aria-label="Delete question">
                        <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M14.74 9l-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 01-2.244 2.077H8.084a2.25 2.25 0 01-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 00-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 013.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 00-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 00-7.5 0"/></svg>
                      </button>
                    </div>
                  }
                </div>
              }
            </div>
          }
        </div>

        <!-- Add Question Form -->
        @if (isEditable() && showAddForm()) {
          <div class="card p-5 border-2 border-brand-200">
            <h3 class="text-sm font-semibold text-surface-700 mb-4">Add Question</h3>
            <form [formGroup]="addFormGroup" (ngSubmit)="addQuestion()" class="space-y-4">
              <div class="grid grid-cols-2 gap-3">
                <div class="col-span-2">
                  <label class="label text-xs">Question Text</label>
                  <input type="text" formControlName="text" class="input text-sm" placeholder="e.g. How would you rate our service?" />
                </div>
                <div>
                  <label class="label text-xs">Type</label>
                  <select formControlName="type" class="input text-sm" (change)="onAddTypeChange()">
                    @for (t of questionTypes; track t.value) {
                      <option [value]="t.value">{{ t.icon }} {{ t.label }}</option>
                    }
                  </select>
                </div>
                <div class="flex items-end pb-1">
                  <label class="flex items-center gap-2 text-sm cursor-pointer">
                    <input type="checkbox" formControlName="isRequired" class="rounded text-brand-600" />
                    Required
                  </label>
                </div>
              </div>

              @if (isChoiceType(addFormGroup.get('type')?.value)) {
                <div>
                  <label class="label text-xs">Options</label>
                  <div formArrayName="options" class="space-y-2">
                    @for (opt of addOptions.controls; track $index; let oi = $index) {
                      <div class="flex gap-2" [formGroupName]="oi">
                        <input type="text" formControlName="text" class="input text-sm flex-1" [placeholder]="'Option ' + (oi + 1)" />
                        @if (addOptions.length > 2) {
                          <button type="button" (click)="removeAddOption(oi)" class="btn-ghost btn-icon btn-sm text-red-400">×</button>
                        }
                      </div>
                    }
                  </div>
                  <button type="button" (click)="addAddOption()" class="btn-secondary btn-sm mt-2">+ Add Option</button>
                </div>
              }

              <div class="flex gap-2">
                <button type="submit" [disabled]="addFormGroup.invalid || saving()" class="btn-primary btn-sm">
                  @if (saving()) { <span class="inline-block w-3 h-3 border-2 border-white/30 border-t-white rounded-full animate-spin"></span> }
                  Add Question
                </button>
                <button type="button" (click)="showAddForm.set(false)" class="btn-secondary btn-sm">Cancel</button>
              </div>
            </form>
          </div>
        }
      }
    </div>

      <!-- ── Clone Questions Modal ───────────────────── -->
      @if (showCloneModal()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40 fade-in">
          <div class="card p-6 w-full max-w-md shadow-modal slide-up">
            <div class="flex items-center justify-between mb-4">
              <div>
                <h3 class="font-semibold text-surface-900">Clone Questions</h3>
                <p class="text-xs text-surface-400 mt-0.5">Copy all questions from another survey into this one</p>
              </div>
              <button (click)="showCloneModal.set(false)" class="btn-ghost btn-icon text-surface-400 text-xl leading-none">×</button>
            </div>

            @if (loadingSurveys()) {
              <div class="space-y-2">
                @for (i of [1,2,3]; track i) {
                  <div class="skeleton h-14 rounded-xl"></div>
                }
              </div>
            } @else if (otherSurveys().length === 0) {
              <div class="text-center py-8">
                <p class="text-surface-400 text-sm">No other surveys available to clone from.</p>
              </div>
            } @else {
              <div class="space-y-2 max-h-72 overflow-y-auto scrollbar-thin pr-1">
                @for (s of otherSurveys(); track s.id) {
                  <button
                    (click)="cloneFrom(s)"
                    [disabled]="cloning()"
                    class="w-full text-left flex items-center gap-3 p-3 rounded-xl border border-surface-200 hover:border-brand-300 hover:bg-brand-50 transition-all group"
                  >
                    <div class="w-9 h-9 rounded-lg bg-surface-100 flex items-center justify-center flex-shrink-0 group-hover:bg-brand-100 transition-colors">
                      <svg class="w-4 h-4 text-surface-400 group-hover:text-brand-600" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"/>
                      </svg>
                    </div>
                    <div class="flex-1 min-w-0">
                      <p class="text-sm font-medium text-surface-800 truncate group-hover:text-brand-700">{{ s.title }}</p>
                      <p class="text-xs text-surface-400">{{ s.state }}</p>
                    </div>
                    @if (cloning() && cloningFromId() === s.id) {
                      <span class="inline-block w-4 h-4 border-2 border-brand-300 border-t-brand-600 rounded-full animate-spin flex-shrink-0"></span>
                    } @else {
                      <span class="text-xs text-brand-600 font-medium opacity-0 group-hover:opacity-100 transition-opacity flex-shrink-0">Clone →</span>
                    }
                  </button>
                }
              </div>
            }

            <div class="flex justify-end mt-4 pt-4 border-t border-surface-100">
              <button (click)="showCloneModal.set(false)" class="btn-secondary btn-sm">Cancel</button>
            </div>
          </div>
        </div>
      }

    <!-- Unified Bottom Bar — always visible when survey is editable -->
    @if (isEditable()) {
      <div class="fixed bottom-0 left-0 right-0 bg-white/95 backdrop-blur border-t border-surface-200 px-4 py-3 flex items-center gap-3 lg:left-[240px]">
        <div class="flex-1 text-sm text-surface-500">
          {{ questions().length }} question{{ questions().length !== 1 ? 's' : '' }}
        </div>
        @if (!showAddForm() && !editingId()) {
          <button (click)="showAddForm.set(true)" class="btn-primary btn-sm">
            <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M12 4v16m8-8H4"/>
            </svg>
            Add Question
          </button>
        }
        <button (click)="openCloneModal()" class="btn-secondary btn-sm">Clone</button>
        <button (click)="openBankModal()" class="btn-secondary btn-sm">From Bank</button>
        <input
          type="file"
          class="hidden"
          #excelPicker
          accept=".xlsx,.xls,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,application/vnd.ms-excel"
          (change)="onExcelFileSelected($event)"
        />
        <button
          type="button"
          (click)="excelPicker.click()"
          [disabled]="excelImporting()"
          class="btn-secondary btn-sm"
          title="Import questions from Excel (.xlsx or .xls)"
        >
          @if (excelImporting()) {
            <span class="inline-block w-3 h-3 border-2 border-surface-400/30 border-t-brand-600 rounded-full animate-spin mr-1 align-middle"></span>
          }
          Import Excel
        </button>
        @if (excelImporting() && excelProgress() > 0) {
          <span class="text-xs text-surface-500 tabular-nums">{{ excelProgress() }}%</span>
        }

        <!-- State action buttons -->
        <button (click)="publish()"
          class="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-semibold border transition-all"
          style="background:#f0fdf4;color:#16a34a;border-color:#bbf7d0">
          ▶ Publish
        </button>

        <a routerLink="/surveys" class="btn-ghost btn-sm text-surface-400">&#8592; Surveys</a>
      </div>
    }

    <!-- Active survey bottom bar -->
    @if (survey()?.state === 'Active') {
      <div class="fixed bottom-0 left-0 right-0 border-t px-4 py-3 flex items-center gap-4 lg:left-[240px]"
        style="background:#16213e;border-color:rgba(255,255,255,0.08)">
        <div class="flex-1">
          <p class="text-sm font-semibold" style="color:#f1f5f9">🟢 Survey is Live</p>
          <p class="text-xs mt-0.5" style="color:#475569">Accepting responses — pause to edit questions</p>
        </div>
        <button (click)="pause()"
          class="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-semibold border transition-all"
          style="background:#fffbeb;color:#d97706;border-color:#fde68a">
          ⏸ Pause
        </button>
        <a routerLink="/surveys" class="btn-secondary btn-sm" style="background:rgba(255,255,255,0.08);color:#94a3b8;border:none">
          &#8592; Surveys
        </a>
      </div>
    }

    <!-- Closed survey bottom bar -->
    @if (survey()?.state === 'Closed') {
      <div class="fixed bottom-0 left-0 right-0 border-t px-4 py-3 flex items-center gap-4 lg:left-[240px]"
        style="background:#16213e;border-color:rgba(255,255,255,0.08)">
        <div class="flex-1">
          <p class="text-sm font-semibold" style="color:#f1f5f9">🔴 Survey is Closed</p>
          <p class="text-xs mt-0.5" style="color:#475569">Reopen to accept responses again</p>
        </div>
        <button (click)="reopen()"
          class="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-semibold border transition-all"
          style="background:#f0fdf4;color:#16a34a;border-color:#bbf7d0">
          ↺ Reopen
        </button>
        <a routerLink="/surveys" class="btn-secondary btn-sm" style="background:rgba(255,255,255,0.08);color:#94a3b8;border:none">
          &#8592; Surveys
        </a>
      </div>
    }

      <!-- ── Import from Question Bank Modal ─────────── -->
      @if (showBankModal()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40 fade-in">
          <div class="card p-6 w-full max-w-lg shadow-modal slide-up max-h-[85vh] flex flex-col">
            <div class="flex items-center justify-between mb-4">
              <div>
                <h3 class="font-semibold text-surface-900">Import from Question Bank</h3>
                <p class="text-xs text-surface-400 mt-0.5">Select individual questions to add to this survey</p>
              </div>
              <button (click)="showBankModal.set(false)" class="btn-ghost btn-icon text-surface-400 text-xl">×</button>
            </div>

            <!-- Search -->
            <div class="mb-3">
              <input
                type="text"
                class="input text-sm w-full"
                placeholder="Search bank questions..."
                [value]="bankSearch"
                (input)="onBankSearch($event)"
              />
            </div>

            <div class="flex-1 overflow-y-auto scrollbar-thin space-y-2 pr-1 min-h-0">
              @if (bankLoading()) {
                @for (i of [1,2,3]; track i) { <div class="skeleton h-14 rounded-xl"></div> }
              } @else if (bankQuestions().length === 0) {
                <div class="text-center py-8">
                  <p class="text-surface-400 text-sm">No bank questions found.</p>
                  <a routerLink="/question-bank" class="text-brand-600 text-xs hover:underline mt-1 inline-block">Go to Question Bank →</a>
                </div>
              } @else {
                @for (q of bankQuestions(); track q.id) {
                  <label
                    class="flex items-start gap-3 p-3 rounded-xl border transition-all cursor-pointer"
                    [class]="selectedBankIds().has(q.id) ? 'border-brand-400 bg-brand-50' : 'border-surface-200 hover:border-brand-200 hover:bg-surface-50'"
                  >
                    <input
                      type="checkbox"
                      [checked]="selectedBankIds().has(q.id)"
                      (change)="toggleBankQuestion(q.id)"
                      class="rounded text-brand-600 mt-0.5"
                    />
                    <div class="flex-1 min-w-0">
                      <p class="text-sm font-medium text-surface-800 truncate">{{ q.text }}</p>
                      <div class="flex items-center gap-2 mt-1">
                        <span class="badge badge-draft text-xs">{{ q.type }}</span>
                        @if (q.tag) {
                          <span class="text-xs px-1.5 py-0.5 rounded-full bg-purple-50 text-purple-600">{{ q.tag }}</span>
                        }
                      </div>
                    </div>
                  </label>
                }
              }
            </div>

            <div class="flex items-center justify-between mt-4 pt-4 border-t border-surface-100">
              <span class="text-xs text-surface-500">{{ selectedBankIds().size }} selected</span>
              <div class="flex gap-2">
                <button (click)="showBankModal.set(false)" class="btn-secondary btn-sm">Cancel</button>
                <button
                  (click)="cloneFromBank()"
                  [disabled]="selectedBankIds().size === 0 || bankCloning()"
                  class="btn-primary btn-sm"
                >
                  @if (bankCloning()) { <span class="inline-block w-3 h-3 border-2 border-white/30 border-t-white rounded-full animate-spin"></span> }
                  Import {{ selectedBankIds().size }} Question{{ selectedBankIds().size !== 1 ? 's' : '' }}
                </button>
              </div>
            </div>
          </div>
        </div>
      }
  `
})
export class SurveyBuilderComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly surveyService = inject(SurveyService);
  private readonly questionService = inject(QuestionService);
  private readonly bankService = inject(QuestionBankService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly router = inject(Router);

  readonly id = input<string>('');
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly survey = signal<Survey | null>(null);
  readonly questions = signal<Question[]>([]);
  readonly editingId = signal<number | null>(null);
  readonly showAddForm = signal(false);
  readonly questionTypes = QUESTION_TYPES;

  // ── Clone Questions ────────────────────────────────
  readonly showCloneModal = signal(false);
  readonly loadingSurveys = signal(false);
  readonly cloning = signal(false);
  readonly cloningFromId = signal<number | null>(null);
  readonly otherSurveys = signal<SurveyListItem[]>([]);

  // ── Import from Question Bank ──────────────────────
  readonly showBankModal = signal(false);
  readonly bankLoading = signal(false);
  readonly bankCloning = signal(false);
  readonly bankQuestions = signal<BankQuestion[]>([]);
  readonly selectedBankIds = signal<Set<number>>(new Set());
  bankSearch = '';

  readonly excelImporting = signal(false);
  readonly excelProgress  = signal(0);

  readonly addFormGroup: FormGroup = this.fb.group({
    text: ['', Validators.required],
    type: ['ShortText'],
    isRequired: [false],
    options: this.fb.array([])
  });

  readonly editFormGroup: FormGroup = this.fb.group({
    text: ['', Validators.required],
    type: ['ShortText'],
    isRequired: [false],
    options: this.fb.array([])
  });

  get addOptions(): FormArray { return this.addFormGroup.get('options') as FormArray; }
  get editOptions(): FormArray { return this.editFormGroup.get('options') as FormArray; }

  // Editable when state is Inactive
  isEditable(): boolean {
    const s = this.survey();
    if (!s) return false;
    return s.state === 'Inactive';
  }
  isChoiceType(type: string): boolean {
    return type === 'SingleChoice' || type === 'MultipleChoice';
  }

  ngOnInit(): void {
    const idNum = Number(this.id());
    this.surveyService.getById(idNum).subscribe({
      next: (s) => { this.survey.set(s); this.loadQuestions(idNum); },
      error: () => { this.loading.set(false); this.router.navigate(['/surveys']); }
    });
  }

  private loadQuestions(surveyId: number): void {
    this.questionService.getAll(surveyId).subscribe({
      next: (qs) => { this.questions.set(qs.sort((a, b) => a.order - b.order)); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  // ─── Add Form ──────────────────────────────────────────
  onAddTypeChange(): void {
    this.clearArray(this.addOptions);
    if (this.isChoiceType(this.addFormGroup.get('type')?.value)) {
      this.addAddOption(); this.addAddOption();
    }
  }

  addAddOption(): void {
    this.addOptions.push(this.fb.group({ text: ['', Validators.required] }));
  }
  removeAddOption(i: number): void { this.addOptions.removeAt(i); }

  addQuestion(): void {
    if (this.addFormGroup.invalid) { this.addFormGroup.markAllAsTouched(); return; }
    const { text, type, isRequired, options } = this.addFormGroup.value;
    const payload = {
      text, type, isRequired,
      order: this.questions().length + 1,
      options: (options as { text: string }[]).map((o, i) => ({ text: o.text, order: i + 1 }))
    };
    this.saving.set(true);
    this.questionService.create(Number(this.id()), payload).subscribe({
      next: (q) => {
        this.questions.update(qs => [...qs, q]);
        this.addFormGroup.reset({ text: '', type: 'ShortText', isRequired: false });
        this.clearArray(this.addOptions);
        this.showAddForm.set(false);
        this.saving.set(false);
        this.toast.success('Question added.');
      },
      error: () => this.saving.set(false)
    });
  }

  // ─── Edit Form ─────────────────────────────────────────
  startEdit(q: Question): void {
    this.editingId.set(q.id);
    this.clearArray(this.editOptions);
    this.editFormGroup.patchValue({ text: q.text, type: q.type, isRequired: q.isRequired });
    q.options?.forEach(opt => {
      this.editOptions.push(this.fb.group({ text: [opt.text, Validators.required] }));
    });
  }

  onEditTypeChange(): void {
    this.clearArray(this.editOptions);
    if (this.isChoiceType(this.editFormGroup.get('type')?.value)) {
      this.addEditOption(); this.addEditOption();
    }
  }

  addEditOption(): void { this.editOptions.push(this.fb.group({ text: ['', Validators.required] })); }
  removeEditOption(i: number): void { this.editOptions.removeAt(i); }

  saveEdit(questionId: number): void {
    if (this.editFormGroup.invalid) { this.editFormGroup.markAllAsTouched(); return; }
    const { text, type, isRequired, options } = this.editFormGroup.value;
    const q = this.questions().find(q => q.id === questionId)!;

    // Backend UpdateQuestionDto — same shape as CreateQuestionDto, uses CreateOptionDto (no id)
    const payload = {
      text, type, isRequired,
      order: q.order,
      options: (options as { text: string }[]).map((o, i) => ({ text: o.text, order: i + 1 }))
    };

    this.saving.set(true);
    this.questionService.update(Number(this.id()), questionId, payload).subscribe({
      next: (updated) => {
        this.questions.update(qs => qs.map(q => q.id === questionId ? updated : q));
        this.editingId.set(null);
        this.saving.set(false);
        this.toast.success('Question updated.');
      },
      error: () => this.saving.set(false)
    });
  }

  cancelEdit(): void { this.editingId.set(null); }

  async deleteQuestion(q: Question): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: 'Delete Question',
      message: `Are you sure you want to delete "${q.text}"? This action cannot be undone.`,
      confirmLabel: 'Delete',
      danger: true
    });
    if (!ok) return;
    this.questionService.delete(Number(this.id()), q.id).subscribe({
      next: () => {
        this.questions.update(qs => qs.filter(x => x.id !== q.id));
        this.toast.success('Question deleted.');
      },
      // BUG-14 FIX: missing error handler — if the API call failed (e.g. survey was
      // activated between the confirm dialog and the request), the local list would be
      // out of sync with the server. Re-load from server to restore accurate state.
      error: () => this.loadQuestions(Number(this.id()))
    });
  }

  publish(): void {
    this.surveyService.setState(Number(this.id()), 'Active').subscribe({
      next: (updated) => {
        this.survey.set(updated);
        this.toast.success('Survey is now Live!');
      },
      error: () => {}
    });
  }

  pause(): void {
    this.surveyService.setState(Number(this.id()), 'Inactive').subscribe({
      next: (updated) => {
        this.survey.set(updated);
        this.toast.success('Survey paused — back to editing.');
      },
      error: () => {}
    });
  }

  reopen(): void {
    this.surveyService.setState(Number(this.id()), 'Inactive').subscribe({
      next: (updated) => {
        this.survey.set(updated);
        this.toast.success('Survey reopened — back to editing.');
      },
      error: () => {}
    });
  }

  openCloneModal(): void {
    this.showCloneModal.set(true);
    this.loadingSurveys.set(true);
    this.surveyService.getAll({ pageNumber: 1, pageSize: 100 }).subscribe({
      next: (page) => {
        // Exclude current survey and non-existent question surveys
        const others = page.items.filter(s => s.id !== Number(this.id()));
        this.otherSurveys.set(others);
        this.loadingSurveys.set(false);
      },
      error: () => this.loadingSurveys.set(false)
    });
  }

  cloneFrom(source: SurveyListItem): void {
    if (this.cloning()) return;
    this.cloning.set(true);
    this.cloningFromId.set(source.id);
    this.surveyService.cloneQuestions(source.id, Number(this.id())).subscribe({
      next: () => {
        this.toast.success(`Questions cloned from "${source.title}"`);
        this.showCloneModal.set(false);
        this.cloning.set(false);
        this.cloningFromId.set(null);
        // Reload questions to show cloned ones
        this.loadQuestions(Number(this.id()));
      },
      error: () => {
        this.cloning.set(false);
        this.cloningFromId.set(null);
      }
    });
  }
  // ── Import from Question Bank ──────────────────────
  openBankModal(): void {
    this.showBankModal.set(true);
    this.selectedBankIds.set(new Set());
    this.bankSearch = '';
    this.loadBankQuestions();
  }

  private bankSearchTimer: ReturnType<typeof setTimeout> | undefined;
  onBankSearch(e: Event): void {
    this.bankSearch = (e.target as HTMLInputElement).value;
    clearTimeout(this.bankSearchTimer);
    this.bankSearchTimer = setTimeout(() => this.loadBankQuestions(), 350);
  }

  private loadBankQuestions(): void {
    this.bankLoading.set(true);
    this.bankService.getAll({
      search: this.bankSearch || undefined,
      pageNumber: 1,
      pageSize: 100
    }).subscribe({
      next: (page) => { this.bankQuestions.set(page.items); this.bankLoading.set(false); },
      error: () => this.bankLoading.set(false)
    });
  }

  toggleBankQuestion(id: number): void {
    this.selectedBankIds.update(ids => {
      const next = new Set(ids);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  }

  cloneFromBank(): void {
    const ids = [...this.selectedBankIds()];
    if (ids.length === 0) return;
    this.bankCloning.set(true);
    this.bankService.cloneIntoSurvey({
      bankQuestionIds: ids,
      targetSurveyId: Number(this.id())
    }).subscribe({
      next: (result) => {
        this.toast.success(`${result.clonedCount} question${result.clonedCount !== 1 ? 's' : ''} imported from bank.`);
        this.showBankModal.set(false);
        this.bankCloning.set(false);
        this.loadQuestions(Number(this.id()));
      },
      error: () => this.bankCloning.set(false)
    });
  }

  onExcelFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file || !this.isEditable()) return;

    const name = file.name.toLowerCase();
    if (!name.endsWith('.xlsx') && !name.endsWith('.xls')) {
      this.toast.error('Please choose an Excel file (.xlsx or .xls). CSV is not supported.');
      return;
    }

    const surveyId = Number(this.id());
    this.excelImporting.set(true);
    this.excelProgress.set(0);

    this.bankService.importExcelWithProgress(file, { surveyId, addToQuestionBank: false }).subscribe({
      next: (ev) => {
        if (ev.type === HttpEventType.UploadProgress && ev.total) {
          this.excelProgress.set(Math.round((100 * ev.loaded) / ev.total));
        } else if (ev.type === HttpEventType.Response) {
          const res = ev as HttpResponse<ApiResponse<QuestionImportResult>>;
          const data = res.body?.data;
          this.excelImporting.set(false);
          this.excelProgress.set(0);
          if (data) {
            const msg =
              data.failed > 0
                ? `Imported ${data.success} of ${data.total} rows (${data.failed} failed).`
                : `Imported ${data.success} question row${data.success !== 1 ? 's' : ''}.`;
            this.toast.success(msg);
            if (data.errors?.length) {
              this.toast.show(data.errors.slice(0, 3).join(' '), 'warning', 8000);
            }
            this.loadQuestions(surveyId);
          }
        }
      },
      error: (err) => {
        this.excelImporting.set(false);
        this.excelProgress.set(0);
        const msg = err?.error?.message ?? err?.message ?? 'Import failed.';
        this.toast.error(typeof msg === 'string' ? msg : 'Import failed.');
      }
    });
  }

  private clearArray(arr: FormArray): void {
    while (arr.length > 0) arr.removeAt(0);
  }
}
