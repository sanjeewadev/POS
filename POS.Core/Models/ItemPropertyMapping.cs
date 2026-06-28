namespace POS.Core.Models
{
    public class ItemPropertyMapping
    {
        // This mapping connects one item variant to one selected value from one group.
        //
        // Example:
        // Variant: Men Short / Red / 32
        //
        // Mapping 1:
        // AttributeGroup: Color
        // AttributeValue: Red
        //
        // Mapping 2:
        // AttributeGroup: Size
        // AttributeValue: 32

        public int ItemVariantId { get; set; }
        public ItemVariant ItemVariant { get; set; } = null!;

        // Stored directly for fast filtering and validation.
        // AppDbContext should enforce:
        // one ItemVariant can have only one value per AttributeGroup.
        public int AttributeGroupId { get; set; }
        public AttributeGroup AttributeGroup { get; set; } = null!;

        public int AttributeValueId { get; set; }
        public AttributeValue AttributeValue { get; set; } = null!;
    }
}