namespace MLMConquerorGlobalEdition.Domain.Interfaces;

public interface ITreeNode
{
    string MemberId { get; set; }
    string? ParentMemberId { get; set; }
    string HierarchyPath { get; set; }
}
