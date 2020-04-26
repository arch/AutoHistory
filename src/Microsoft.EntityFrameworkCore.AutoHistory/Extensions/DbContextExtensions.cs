// Copyright (c) Arch team. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Newtonsoft.Json.Linq;

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
        /// <param name="useAddState">EntityStatus.Added will user too</param>
        public static void EnsureAutoHistory(this DbContext context, Func<EntityState, bool> predicate)
        {
            EnsureAutoHistory<AutoHistory>(context, () => new AutoHistory(), useAddState);
        }

        public static void EnsureAutoHistory<TAutoHistory>(this DbContext context, Func<TAutoHistory> createHistoryFactory, bool useAddState = false)
            where TAutoHistory : AutoHistory
        {
            // Must ToArray() here for excluding the AutoHistory model.
            // Currently, only support Modified and Deleted entity.
            var allEntries = context.ChangeTracker.Entries();
            IEnumerable<EntityEntry> entries = null;
            if (useAddState)
            {
                entries = allEntries.Where(e=>e.State == EntityState.Modified || e.State == EntityState.Deleted || e.State == EntityState.Added).ToArray();
            }
            else
            {
                entries = allEntries.Where(e=>e.State == EntityState.Modified || e.State == EntityState.Deleted).ToArray();
            }

            foreach (var entry in entries)
            {
                context.Add<TAutoHistory>(entry.AutoHistory(createHistoryFactory));
            }
        }

        internal static TAutoHistory AutoHistory<TAutoHistory>(this EntityEntry entry, Func<TAutoHistory> createHistoryFactory)
            where TAutoHistory : AutoHistory
        {
            var history = createHistoryFactory();
            history.TableName = entry.Metadata.Relational().TableName;

            // Get the mapped properties for the entity type.
            // (include shadow properties, not include navigations & references)
            var properties = entry.Properties;

            var formatting = AutoHistoryOptions.Instance.JsonSerializerSettings.Formatting;
            var jsonSerializer = AutoHistoryOptions.Instance.JsonSerializer;
            var json = new JObject();
            switch (entry.State)
            {
                case EntityState.Added:
                    var aftAdded = new JObject();
                    foreach (var prop in properties)
                    {
                        if (prop.Metadata.IsKey() || prop.Metadata.IsForeignKey())
                        {
                            continue;
                        }
                        aftAdded[prop.Metadata.Name] = prop.CurrentValue != null
                            ? JToken.FromObject(prop.CurrentValue, jsonSerializer)
                            : JValue.CreateNull();
                    }

                    // REVIEW: what's the best way to set the RowId?
                    json["before"] = null;
                    json["after"] = aftAdded;

                    history.RowId = "0";
                    history.Kind = EntityState.Added;
                    history.Changed = json.ToString(formatting);
                    break;
                case EntityState.Modified:
                    var befModified = new JObject();
                    var aftModified = new JObject();

                    foreach (var prop in properties)
                    {
                        if (prop.IsModified)
                        {
                            if (prop.OriginalValue != null)
                            {
                                if (prop.OriginalValue != prop.CurrentValue)
                                {
                                    befModified[prop.Metadata.Name] = JToken.FromObject(prop.OriginalValue, jsonSerializer);
                                }
                                else
                                {
                                    var originalValue = entry.GetDatabaseValues().GetValue<object>(prop.Metadata.Name);
                                    befModified[prop.Metadata.Name] = originalValue != null
                                        ? JToken.FromObject(originalValue, jsonSerializer)
                                        : JValue.CreateNull();
                                }
                            }
                            else
                            {
                                befModified[prop.Metadata.Name] = JValue.CreateNull();
                            }

                            aftModified[prop.Metadata.Name] = prop.CurrentValue != null
                            ? JToken.FromObject(prop.CurrentValue, jsonSerializer)
                            : JValue.CreateNull();
                        }
                    }

                    json["before"] = befModified;
                    json["after"] = aftModified;

                    history.RowId = entry.PrimaryKey();
                    history.Kind = EntityState.Modified;
                    history.Changed = json.ToString(formatting);
                    break;
                case EntityState.Deleted:
                    var beforeDeleted = new JObject();

                    json["before"] = beforeDeleted;
                    json["after"] = null;

                    foreach (var prop in properties)
                    {
                        beforeDeleted[prop.Metadata.Name] = prop.OriginalValue != null
                            ? JToken.FromObject(prop.OriginalValue, jsonSerializer)
                            : JValue.CreateNull();
                    }
                    json["before"] = beforeDeleted;
                    json["after"] = null;
                    history.RowId = entry.PrimaryKey();
                    history.Kind = EntityState.Deleted;
                    history.Changed = json.ToString(formatting);
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
