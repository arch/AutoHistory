// Copyright (c) Arch team. All rights reserved.

namespace Microsoft.EntityFrameworkCore
{
    /// <summary>
    /// Represents a plugin for Microsoft.EntityFrameworkCore to support automatically recording data changes history.
    /// </summary>
    public static class ModelBuilderExtensions
    {
        /// <summary>
        /// Enables the automatic recording change history.
        /// </summary>
        /// <param name="modelBuilder">The <see cref="ModelBuilder"/> to enable auto history functionality.</param>
        /// <returns>The <see cref="ModelBuilder"/> to enable auto history functionality.</returns>
        public static ModelBuilder EnableAutoHistory(this ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AutoHistory>(b =>
            {
                b.Property(c => c.RowId).IsRequired().HasMaxLength(50);
                b.Property(c => c.TableName).IsRequired().HasMaxLength(128);
                b.Property(c => c.EntityName).IsRequired().HasMaxLength(128);
                //b.Property(c => c.Changed).HasMaxLength(2048);
                // This MSSQL only
                //b.Property(c => c.Created).HasDefaultValueSql("getdate()");
            });
            
            return modelBuilder;
        }
    }
}
