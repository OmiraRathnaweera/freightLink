using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FreightLink.Api.Data.Configurations;

public class InvoiceLineItemConfiguration : IEntityTypeConfiguration<InvoiceLineItem>
{
    public void Configure(EntityTypeBuilder<InvoiceLineItem> builder)
    {
        builder.HasKey(x => x.InvoiceLineItemId);
        builder.Property(x => x.InvoiceLineItemId).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Description).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Quantity).HasPrecision(12, 2);
        builder.Property(x => x.UnitPrice).HasPrecision(12, 2);
        builder.Property(x => x.TaxRate).HasPrecision(5, 2);
        builder.Property(x => x.Amount).HasPrecision(12, 2);
        builder.Property(x => x.SortOrder).HasDefaultValue(0);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_invoice_line_item_qty", "\"Quantity\" > 0");
            t.HasCheckConstraint("ck_invoice_line_item_unit_price", "\"UnitPrice\" >= 0");
            t.HasCheckConstraint("ck_invoice_line_item_tax_rate", "\"TaxRate\" >= 0");
            t.HasCheckConstraint("ck_invoice_line_item_amount", "\"Amount\" >= 0");
        });
    }
}
