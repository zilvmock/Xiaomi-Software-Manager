using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace xsm.Models
{
	[Index(nameof(Name), IsUnique = true)]
	public class Region
	{
		public int Id { get; set; }
		
		[Required]
		[MaxLength(24)]
		public required string Name { get; set; }

		[Required]
		[MaxLength(12)]
		public required string Acronym { get; set; }

		public ICollection<Software> Software { get; set; } = new HashSet<Software>();
		
	}
}
