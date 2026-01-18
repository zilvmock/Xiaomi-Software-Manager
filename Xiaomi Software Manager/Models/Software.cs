using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace xsm.Models
{
	public class Software
	{
		public int Id { get; set; }
		
		[Required]
		[MaxLength(64)]
		public required string Name { get; set; }

		[MaxLength(64)]
		public string? Codename { get; set; }

		public ICollection<Region> Regions { get; set; } = new HashSet<Region>();

		[Url]
		public string? WebLink { get; set; }

		[MaxLength(64)]
		public string? WebVersion { get; set; }

		[MaxLength(64)]
		public string? LocalVersion { get; set; }
		
		[DefaultValue(false)]
		public bool IsUpToDate { get; set; }
		
		[DefaultValue(false)]
		public bool IsDownloading { get; set; }

		[NotMapped]
		public string RegionDisplay => Regions.Count == 0
			? string.Empty
			: string.Join(", ", Regions.Select(region => region.Acronym));
	}
}
