using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;

public class PodiumModel : PageModel
{
    public class Player {
        public int Id {get;set;}
        public string Username {get;set;}
        public int Score {get;set;}
        public string AvatarUrl {get;set;} // null => show initials
        public int Rank {get;set;}
    }

    public List<Player> Players { get; set; } = new();
    public string Variant { get; set; } = "pop"; // or load from query/admin settings

    public void OnGet(string variant = null)
    {
        Variant = variant ?? "pop";
        // Mock data (replace with real data source)
        Players = new List<Player> {
            new Player{Id=1, Username="Burak", Score=1420, AvatarUrl=null, Rank=1},
            new Player{Id=2, Username="Elif", Score=1180, AvatarUrl=null, Rank=2},
            new Player{Id=3, Username="Mert", Score=900, AvatarUrl=null, Rank=3}
        };

        // Ensure ordering for the partials (0->1st,1->2nd,2->3rd)
    }
}
