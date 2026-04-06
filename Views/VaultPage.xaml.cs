using StudySync.Models;
using StudySync.ViewModels;

namespace StudySync.Views;

public partial class VaultPage : ContentPage
{
    public VaultPage()
    {
        InitializeComponent();
        BindingContext = new VaultViewModel();

        NotesCollection.SelectionChanged += async (s, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is Note selectedNote)
            {
                NotesCollection.SelectedItem = null;
                System.Diagnostics.Debug.WriteLine($"?? SelectNote fired from code-behind. ID: {selectedNote.Id}");
                await Shell.Current.GoToAsync($"{nameof(NoteDetailPage)}?noteId={selectedNote.Id}");
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is VaultViewModel vm)
        {
            await vm.LoadNotesAsync();
        }
    }
}