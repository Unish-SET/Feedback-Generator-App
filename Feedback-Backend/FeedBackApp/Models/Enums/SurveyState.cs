namespace FeedBackApp.Models.Enums
{
    /// <summary>
    /// Single source of truth for a survey's lifecycle state.
    /// Inactive = editable, Active = live (accepts responses), Closed = permanently locked.
    /// </summary>
    public enum SurveyState
    {
        Inactive = 0,
        Active   = 1,
        Closed   = 2
    }
}
