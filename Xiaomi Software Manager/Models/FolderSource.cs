using System.ComponentModel.DataAnnotations;

namespace xsm.Models
{
	public class FolderSource
	{
		public int Id { get; set; }

		[Required]
		[MaxLength(32)]
		public required string Name { get; set; }

		[Required]
		[MaxLength(2048)]
		public required string Path { get; set; }
		
	}
}
