// ─── Auth ──────────────────────────────────────────────────────────────────
export interface LoginRequest {
  username: string;
  password: string;
}

export interface RegisterRequest {
  username: string;
  email: string;
  password: string;
}

// Backend returns: { success: true, data: { token: "...", userId, username, email, role } }
export interface AuthResponse {
  token: string;
  // ALIGN-03: added — no longer need to JWT-decode to get user identity
  userId: number;
  username: string;
  email: string;
  role: 'Admin' | 'Creator';
}

export interface AuthUser {
  userId: number;
  username: string;
  email: string;
  role: 'Admin' | 'Creator';
  exp: number;
}

// ─── Backend envelope ──────────────────────────────────────────────────────
export interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
}

// ─── Pagination ────────────────────────────────────────────────────────────
// Backend PaginatedResult<T>
export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

// ─── Survey ────────────────────────────────────────────────────────────────
export type SurveyState = 'Inactive' | 'Active' | 'Closed';

// Backend: SurveyListDto (from GetAll)
export interface SurveyListItem {
  id: number;
  title: string;
  /** Single source of truth: Inactive | Active | Closed */
  state: SurveyState;
  publicToken: string;
  responseCount: number;
  createdAt: string;
  createdBy: number;
  creatorName: string;
}

// Backend: SurveyResponseDto (from GetById / Create / Update)
export interface Survey {
  id: number;
  title: string;
  description: string;
  publicToken: string;
  /** Single source of truth: Inactive | Active | Closed */
  state: SurveyState;
  startDate?: string;
  endDate?: string;
  allowAnonymous: boolean;
  isInviteOnly: boolean;
  createdBy: number;
  creatorName: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateSurveyRequest {
  title: string;
  description: string;
  allowAnonymous: boolean;
  startDate?: string | null;
  endDate?: string | null;
  /** Optional: pass 'Active' to publish immediately on creation. */
  state?: 'Active';
}

export interface UpdateSurveyRequest {
  title: string;
  description: string;
  allowAnonymous: boolean;
  startDate?: string | null;
  endDate?: string | null;
}

// ─── Questions ─────────────────────────────────────────────────────────────// Backend: OptionResponseDto
export interface QuestionOption {
  id: number;
  text: string;
  order: number;
}

// Backend: QuestionResponseDto
export interface Question {
  id: number;
  text: string;
  type: string;
  isRequired: boolean;
  order: number;
  options: QuestionOption[];
}

// Backend: CreateQuestionDto
export interface CreateQuestionRequest {
  text: string;
  type: string;
  isRequired: boolean;
  order: number;
  options: { text: string; order: number }[];
}

// Backend: UpdateQuestionDto (uses CreateOptionDto for options — no id)
export interface UpdateQuestionRequest {
  text: string;
  type: string;
  isRequired: boolean;
  order: number;
  options: { text: string; order: number }[];
}

// ─── Public Survey ─────────────────────────────────────────────────────────
// Backend: PublicSurveyDto
export interface PublicSurvey {
  title: string;
  description: string;
  allowAnonymous: boolean;
  isInviteOnly: boolean;
  questions: PublicQuestion[];
}

// Backend: PublicQuestionDto
export interface PublicQuestion {
  id: number;
  text: string;
  type: string;
  isRequired: boolean;
  order: number;
  options: QuestionOption[];
}

// ─── Responses ─────────────────────────────────────────────────────────────
export interface SubmitResponseRequest {
  answers: SubmitAnswerRequest[];
}

// Backend: SubmitAnswerDto
export interface SubmitAnswerRequest {
  questionId: number;
  selectedOptionId?: number;         // SingleChoice — singular int
  textValue?: string;                // ShortText / LongText
  ratingValue?: number;              // RatingScale
  selectedOptionIds?: number[];      // MultipleChoice — list
}

// Backend: AnswerDto
export interface AnswerDto {
  questionId: number;
  questionText: string;
  questionType: string;
  selectedOptionId?: number;
  selectedOptionText?: string;
  textValue?: string;
  ratingValue?: number;
  selectedOptionIds?: number[];
}

export interface SurveyResponseRecord {
  id: number;
  surveyId: number;
  userId?: number;
  username?: string;
  submittedAt: string;
  answers: AnswerDto[];
}

// ─── Analytics ─────────────────────────────────────────────────────────────
export interface SurveyAnalytics {
  surveyId: number;
  surveyTitle: string;
  totalResponses: number;
  questions: QuestionAnalytic[];
  dateWiseCounts: DateWiseCount[];
}

// Backend: QuestionAnalyticsDto
export interface QuestionAnalytic {
  questionId: number;
  questionText: string;
  questionType: string;
  averageRating?: number | null;
  optionDistributions: OptionDistribution[];   // backend: OptionDistributions
  textResponses: string[];                     // backend: TextResponses
}

// Backend: OptionDistributionDto
export interface OptionDistribution {
  optionId: number;
  optionText: string;
  count: number;
  percentage: number;
}

export interface DateWiseCount {
  date: string;
  count: number;
}

// ─── Users ─────────────────────────────────────────────────────────────────
// Backend: UserResponseDto
export interface AppUser {
  id: number;
  username: string;
  email: string;
  role: string;
  isActive: boolean;
  isDeleted: boolean;
  createdAt: string;
  surveyCount: number;
  responseCount: number;
}

// Backend: UpdateUserRoleDto — role is plain string
export interface UpdateRoleRequest {
  role: string;
}

// ─── Toast ─────────────────────────────────────────────────────────────────
export type ToastType = 'success' | 'error' | 'info' | 'warning';

export interface Toast {
  id: string;
  type: ToastType;
  message: string;
  duration?: number;
}

// ─── Audit ─────────────────────────────────────────────────────────────────
export interface AuditLog {
  id: string;
  userId?: number;
  username: string;
  action: string;
  entityName: string;
  entityId?: string;
  changes?: string;
  ipAddress?: string;
  correlationId?: string;
  timestamp: string;
}

export interface AuditMeta {
  actions: string[];
  entities: string[];
}

// ─── Admin Survey ──────────────────────────────────────────────────────────
export interface AdminSurveyListItem {
  id: number;
  title: string;
  status: string;
  createdBy: string;
  creatorEmail: string;
  createdAt: string;
  isDeleted: boolean;
  responseCount: number;
}

export interface AdminSurveyDetail {
  id: number;
  title: string;
  description: string;
  status: string;
  createdBy: string;
  creatorEmail: string;
  createdAt: string;
  updatedAt: string;
  isDeleted: boolean;
  allowAnonymous: boolean;
  startDate?: string;
  endDate?: string;
  totalResponses: number;
  questionCount: number;
}

export interface AdminStats {
  totalSurveys: number;
  activeSurveys: number;
  deletedSurveys: number;
  totalResponses: number;
}

// ─── Question Bank ─────────────────────────────────────────────────────────
export interface BankQuestion {
  id: number;
  text: string;
  type: string;
  isRequired: boolean;
  tag?: string;
  createdAt: string;
  options: QuestionOption[];
}

export interface CreateBankQuestionRequest {
  text: string;
  type: string;
  isRequired: boolean;
  tag?: string;
  options: { text: string; order: number }[];
}

export interface CloneFromBankRequest {
  bankQuestionIds: number[];
  targetSurveyId: number;
}

export interface CloneFromBankResult {
  newQuestionIds: number[];
  clonedCount: number;
  message: string;
}

// ─── Import ────────────────────────────────────────────────────────────────
export interface QuestionImportResult {
  total: number;
  success: number;
  failed: number;
  errors: string[];
}


// ─── Invites ───────────────────────────────────────────────────────────────
export interface SurveyInviteItem {
  id: number;
  email: string;
  isUsed: boolean;
  sentAt: string;
}

// ─── OTP ───────────────────────────────────────────────────────────────────
export interface OtpVerifiedResponse {
  sessionToken: string;
  expiresAt: string;
}
