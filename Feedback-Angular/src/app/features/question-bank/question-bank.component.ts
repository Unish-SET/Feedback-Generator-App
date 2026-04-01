import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormArray, Validators, FormGroup } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { QuestionBankService } from '../../core/services/question-bank.service';
import { ToastService } from '../../core/services/toast.service';
import { ConfirmDialogService } from '../../shared/services/confirm-dialog.service';
import { BankQuestion, CreateBankQuestionRequest, PaginatedResult } from '../../shared/models';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';

const QUESTION_TYPES = [
  { value: 'ShortText',      label: 'Short Text',      icon: '📝' },
  { value: 'LongText',       label: 'Long Text',       icon: '📄' },
  { value: 'SingleChoice',   label: 'Single Choice',   icon: '⚪' },
  { value: 'MultipleChoice', label: 'Multiple Choice', icon: '☑️' },
  { value: 'RatingScale',    label: 'Rating Scale',    icon: '⭐' }
];

@Component({
  selector: 'app-question-bank',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, EmptyStateComponent],
  templateUrl: './question-bank.component.html'
})
export class QuestionBankComponent implements OnInit {
  private readonly bankService = inject(QuestionBankService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly fb = inject(FormBuilder);

  readonly questionTypes = QUESTION_TYPES;

  // ── State ──────────────────────────────────────────
  readonly loading = signal(true);
  readonly filtering = signal(false);
  readonly saving = signal(false);
  readonly questions = signal<BankQuestion[]>([]);
  readonly currentPage = signal(1);
  readonly totalPages = signal(1);
  readonly totalCount = signal(0);
  readonly pageSize = 5;
  readonly knownTags = signal<string[]>([]);

  readonly showFormModal = signal(false);
  readonly editingId = signal<number | null>(null);

  readonly showImportModal = signal(false);
  readonly importFile = signal<File | null>(null);
  readonly importing = signal(false);
  readonly importResult = signal<{ success: number; failed: number; errors: string[] } | null>(null);
  addToBankOnImport = true;

  // Filters
  searchTerm = '';
  tagFilter = '';
  typeFilter = '';

  // Form
  readonly formGroup: FormGroup = this.fb.group({
    text: ['', Validators.required],
    type: ['ShortText'],
    isRequired: [false],
    tag: [''],
    options: this.fb.array([])
  });

  get optionsArray(): FormArray { return this.formGroup.get('options') as FormArray; }

  ngOnInit(): void {
    this.load();
  }

  // ── Data Loading ───────────────────────────────────
  private load(isFilter = false): void {
    if (isFilter) this.filtering.set(true);
    else this.loading.set(true);
    this.bankService.getAll({
      search: this.searchTerm || undefined,
      tag: this.tagFilter || undefined,
      type: this.typeFilter || undefined,
      pageNumber: this.currentPage(),
      pageSize: this.pageSize
    }).subscribe({
      next: (page) => {
        this.questions.set(page.items);
        this.totalCount.set(page.totalCount);
        this.totalPages.set(page.totalPages || 1);
        const tags = new Set(this.knownTags());
        page.items.forEach(q => { if (q.tag) tags.add(q.tag); });
        this.knownTags.set([...tags].sort());
        this.loading.set(false);
        this.filtering.set(false);
      },
      error: () => { this.loading.set(false); this.filtering.set(false); }
    });
  }

  // ── Filters ────────────────────────────────────────
  private searchTimer: any;
  onSearch(e: Event): void {
    this.searchTerm = (e.target as HTMLInputElement).value;
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => { this.currentPage.set(1); this.load(true); }, 350);
  }

  onTagChange(e: Event): void {
    this.tagFilter = (e.target as HTMLSelectElement).value;
    this.currentPage.set(1);
    this.load(true);
  }

  onTypeChange(e: Event): void {
    this.typeFilter = (e.target as HTMLSelectElement).value;
    this.currentPage.set(1);
    this.load(true);
  }

  changePage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.currentPage.set(page);
    this.load(true);
  }

  // ── CRUD ───────────────────────────────────────────
  openAddModal(): void {
    this.editingId.set(null);
    this.formGroup.reset({ text: '', type: 'ShortText', isRequired: false, tag: '' });
    this.clearOptions();
    this.showFormModal.set(true);
  }

  openEditModal(q: BankQuestion): void {
    this.editingId.set(q.id);
    this.formGroup.patchValue({ text: q.text, type: q.type, isRequired: q.isRequired, tag: q.tag ?? '' });
    this.clearOptions();
    q.options?.forEach(opt => {
      this.optionsArray.push(this.fb.group({ text: [opt.text, Validators.required] }));
    });
    this.showFormModal.set(true);
  }

  onTypeFormChange(): void {
    this.clearOptions();
    if (this.isChoiceType(this.formGroup.get('type')?.value)) {
      this.addOption(); this.addOption();
    }
  }

  addOption(): void {
    this.optionsArray.push(this.fb.group({ text: ['', Validators.required] }));
  }
  removeOption(i: number): void { this.optionsArray.removeAt(i); }

  saveQuestion(): void {
    if (this.formGroup.invalid) { this.formGroup.markAllAsTouched(); return; }
    const { text, type, isRequired, tag, options } = this.formGroup.value;
    const payload: CreateBankQuestionRequest = {
      text, type, isRequired,
      tag: tag || undefined,
      options: (options as { text: string }[]).map((o, i) => ({ text: o.text, order: i + 1 }))
    };

    this.saving.set(true);
    const editId = this.editingId();
    const op = editId
      ? this.bankService.update(editId, payload)
      : this.bankService.create(payload);

    op.subscribe({
      next: () => {
        this.toast.success(editId ? 'Question updated.' : 'Question created.');
        this.showFormModal.set(false);
        this.saving.set(false);
        this.load();
      },
      error: () => this.saving.set(false)
    });
  }

  async deleteQuestion(q: BankQuestion): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: 'Delete Question',
      message: `Are you sure you want to delete "${q.text}"? This action cannot be undone.`,
      confirmLabel: 'Delete',
      danger: true
    });
    if (!ok) return;
    this.bankService.delete(q.id).subscribe({
      next: () => { this.toast.success('Question deleted.'); this.load(); },
      error: () => {}
    });
  }

  // ── Excel Import ───────────────────────────────────
  onFileSelect(e: Event): void {
    const f = (e.target as HTMLInputElement).files?.[0];
    if (f) this.importFile.set(f);
  }

  onFileDrop(e: DragEvent): void {
    e.preventDefault();
    const f = e.dataTransfer?.files?.[0];
    if (f) this.importFile.set(f);
  }

  importExcel(): void {
    const file = this.importFile();
    if (!file) return;
    this.importing.set(true);
    this.importResult.set(null);
    this.bankService.importExcel(file, undefined, this.addToBankOnImport).subscribe({
      next: (result) => {
        this.importResult.set({ success: result.success, failed: result.failed, errors: result.errors });
        this.importing.set(false);
        if (result.success > 0) {
          this.toast.success(`${result.success} questions imported.`);
          this.load();
        }
      },
      error: () => this.importing.set(false)
    });
  }

  downloadTemplate(): void {
    this.bankService.downloadTemplate().subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = 'question_import_template.xlsx';
        a.click(); URL.revokeObjectURL(url);
      }
    });
  }

  // ── Helpers ────────────────────────────────────────
  isChoiceType(type: string): boolean {
    return type === 'SingleChoice' || type === 'MultipleChoice';
  }

  getTypeIcon(type: string): string {
    return this.questionTypes.find(t => t.value === type)?.icon ?? '❓';
  }

  private clearOptions(): void {
    while (this.optionsArray.length > 0) this.optionsArray.removeAt(0);
  }
}
