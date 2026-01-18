using System.Collections.Generic;
using System.ComponentModel;

namespace xsm.ViewModels;

public sealed class SoftwareGridState
{
	public string? SearchText { get; init; }

	public bool RegionAllSelected { get; init; } = true;

	public List<string> SelectedRegions { get; init; } = new();

	public string? SortMemberPath { get; init; }

	public ListSortDirection? SortDirection { get; init; }
}
