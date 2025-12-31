using LanMessenger.Models;
using LanMessenger.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LanMessenger.Pages;

public class IndexModel : PageModel
{
    //private readonly ILogger<IndexModel> _logger;

    private readonly MessageStore _store;

    public IndexModel(MessageStore store) => _store = store;

    public IReadOnlyList<ChatMessage> Messages { get; private set; } = [];


    public void OnGet()
    {
        Messages = _store.GetLatest();
    }    

}
