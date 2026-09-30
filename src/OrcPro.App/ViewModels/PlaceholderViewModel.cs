namespace OrcPro.App.ViewModels;

public class PlaceholderViewModel : ViewModelBase
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = "Esta funcionalidade será implementada em breve.";

    public PlaceholderViewModel() { }

    public PlaceholderViewModel(string title, string description = "")
    {
        Title = title;
        if (!string.IsNullOrWhiteSpace(description))
            Description = description;
    }
}
