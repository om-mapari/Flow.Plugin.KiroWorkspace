using System.Collections.ObjectModel;

namespace Flow.Plugin.KiroWorkspace
{
    public class Settings
    {
        public bool DiscoverWorkspaces { get; set; } = true;

        public ObservableCollection<string> CustomWorkspaces { get; set; } = new();
    }
}
