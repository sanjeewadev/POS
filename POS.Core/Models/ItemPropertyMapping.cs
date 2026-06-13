using System;

namespace POS.Core.Models
{
    public class ItemPropertyMapping
    {
        // Composite Key will be defined in AppDbContext (ItemVariantId + AttributeValueId)

        public int ItemVariantId { get; set; }
        public ItemVariant ItemVariant { get; set; } = null!;

        // Storing the GroupId here acts as an incredibly fast database index
        // so we don't have to JOIN three tables just to find out what "Group" the value belongs to.
        public int AttributeGroupId { get; set; }
        public AttributeGroup AttributeGroup { get; set; } = null!;

        public int AttributeValueId { get; set; }
        public AttributeValue AttributeValue { get; set; } = null!;
    }
}