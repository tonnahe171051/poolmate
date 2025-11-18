﻿using System.ComponentModel.DataAnnotations;

namespace PoolMate.Api.Models
{
    public class Player
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string FullName { get; set; } = default!;
        public string? Nickname { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        [MaxLength(2)]
        public string? Country { get; set; }
        public string? City { get; set; }  
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }
        public int? SkillLevel { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<TournamentPlayer> TournamentPlayers { get; set; } = new List<TournamentPlayer>();
    }
}
