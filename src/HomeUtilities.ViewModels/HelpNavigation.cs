namespace HomeUtilities.ViewModels;

/// <summary>Opens the user guide at the section of a topic; shared by every screen with a "?" button.</summary>
public static class HelpNavigation
{
    /// <summary>Navigation parameter carrying the <see cref="HelpTopic"/> to scroll to.</summary>
    public const string TopicParameter = "topic";

    public static Task OpenAsync(INavigationService navigation, HelpTopic topic) =>
        navigation.GoToAsync(Routes.HelpSection, new Dictionary<string, object> { [TopicParameter] = topic });
}
