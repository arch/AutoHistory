// Copyright (c) Arch team. All rights reserved.

using System;

namespace Microsoft.EntityFrameworkCore
{
    /// <summary>
    /// Represents the entity change history.
    /// </summary>
    public class AutoHistory
    {
        /// <summary>
        /// Gets or sets the primary key.
        /// </summary>
        /// <value>The id.</value>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the source row id.
        /// </summary>
        /// <value>The source row id.</value>
        public string RowId { get; set; }

        /// <summary>
        /// Gets or sets the name of the table.
        /// </summary>
        /// <value>The name of the table.</value>
        public string TableName { get; set; }

        /// <summary>
        /// Gets or sets the fully qualified name of the entity who represents a database table.
        /// </summary>
        /// <value>The fullname of the entity.</value>
        public string EntityName { get; set; }

        /// <summary>
        /// Gets or sets the json about the changing.
        /// </summary>
        /// <value>The json about the changing.</value>
        public string Changed { get; set; }

        /// <summary>
        /// Gets or sets the change kind.
        /// </summary>
        /// <value>The change kind.</value>
        public EntityState Kind { get; set; }

        /// <summary>
        /// Gets or sets the create time.
        /// </summary>
        /// <value>The create time.</value>
        public DateTime Created { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the ID of parent historical change.
        /// </summary>
        /// <value>The create time.</value>
        public int? ParentId { get; set; }
        public virtual AutoHistory Parent { get; set; }
    }
}
