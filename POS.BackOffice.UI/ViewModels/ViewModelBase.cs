using CommunityToolkit.Mvvm.ComponentModel;

namespace POS.BackOffice.UI.ViewModels
{
    // Inheriting from ObservableObject hooks us into the Community Toolkit magic
    public abstract class ViewModelBase : ObservableObject
    {
        // Later, we can add global properties here that EVERY page needs, 
        // such as an "IsLoading" spinner or a "PageTitle" string.
    }
}