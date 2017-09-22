// Copyright (c) Arch team. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Microsoft.EntityFrameworkCore
{
    /// <summary>
    /// Represents a plugin for Microsoft.EntityFrameworkCore to support automatically recording data changes history.
    /// </summary>
    public static class DbContextExtensions
    {
        /// <summary>
        /// Ensures the automatic history.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="creator">User's name or email</param>
        /// <param name="ipAddress">User's IP address</param>
        public static void EnsureAutoHistory(this DbContext context, string creator = null, string ipAddress = null)
        {
            // Must ToArray() here for excluding the AutoHistory model.
            // Currently, only support Modified and Deleted entity.
            var entries = context.ChangeTracker.Entries().Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted).ToArray();
            foreach (var entry in entries)
            {
                context.Add(entry.AutoHistory(creator, ipAddress));
            }
        }

        internal static AutoHistory AutoHistory(this EntityEntry entry, string creator = null, string ipAddress = null)
        {
            var history = new AutoHistory
            {
                TableName = entry.Metadata.Relational().TableName,
                EntityName = entry.Entity.GetType().Name,
                CreatedBy = creator,
                IPAddress = ipAddress
            };

            var js = JsonSerializer.Create(new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Formatting = Formatting.Indented,
                NullValueHandling=NullValueHandling.Include
            });
            

            // Get the mapped properties for the entity type.
            // (include shadow properties, not include navigations & references)
            var properties = entry.Properties;

            var json = new JObject();
            switch (entry.State)
            {
                case EntityState.Added:
                    foreach (var prop in properties)
                    {
                        if (prop.Metadata.IsKey() || prop.Metadata.IsForeignKey())
                        {
                            continue;
                        }
                        json[prop.Metadata.Name] = JToken.FromObject(prop.CurrentValue, js);
                    }

                    // REVIEW: what's the best way to set the RowId?
                    history.RowId = "0";
                    history.Kind = EntityState.Added;
                    history.Changed = json.ToString();
                    break;
                case EntityState.Modified:
                    var bef = new JObject();
                    var aft = new JObject();

                    foreach (var prop in properties)
                    {
                        if (prop.IsModified)
                        {
                            bef[prop.Metadata.Name] = JToken.FromObject(prop.OriginalValue, js);
                            aft[prop.Metadata.Name] = JToken.FromObject(prop.CurrentValue, js);
                        }
                    }

                    history.RowId = entry.PrimaryKey();
                    history.Kind = EntityState.Modified;
                    history.BeforeChange = bef.ToString();
                    history.Changed = aft.ToString();
                    break;
                case EntityState.Deleted:
                    foreach (var prop in properties)
                    {
                        json[prop.Metadata.Name] = JToken.FromObject(prop.OriginalValue, js);
                    }
                    history.RowId = entry.PrimaryKey();
                    history.Kind = EntityState.Deleted;
                    history.Changed = json.ToString();
                    break;
                case EntityState.Detached:
                case EntityState.Unchanged:
                default:
                    throw new NotSupportedException("AutoHistory only support Deleted and Modified entity.");
            }

            return history;
        }

        private static string PrimaryKey(this EntityEntry entry)
        {
            var key = entry.Metadata.FindPrimaryKey();

            var values = new List<object>();
            foreach (var property in key.Properties)
            {
                var value = entry.Property(property.Name).CurrentValue;
                if (value != null)
                {
                    values.Add(value);
                }
            }

            return string.Join(",", values);
        }
    }
}
