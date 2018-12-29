// Copyright (c) Arch team. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Internal;

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
        public static void EnsureAutoHistory(this DbContext context)
        {
            EnsureAutoHistory<AutoHistory>(context, () => new AutoHistory());
        }

        public static void EnsureAutoHistory<TAutoHistory>(this DbContext context, Func<TAutoHistory> createHistoryFactory)
            where TAutoHistory : AutoHistory
        {
            // Must ToArray() here for excluding the AutoHistory model.
            // Currently, only support Modified and Deleted entity.
            var entries = context.ChangeTracker.Entries().Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted).ToArray();
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
                    foreach (var prop in properties)
                    {
                        if (prop.Metadata.IsKey() || prop.Metadata.IsForeignKey())
                        {
                            continue;
                        }
                        json[prop.Metadata.Name] = prop.CurrentValue != null
                            ? JToken.FromObject(prop.CurrentValue, jsonSerializer)
                            : JValue.CreateNull();
                    }

                    // REVIEW: what's the best way to set the RowId?
                    history.RowId = "0";
                    history.Kind = EntityState.Added;
                    history.Changed = json.ToString(formatting);
                    break;
                case EntityState.Modified:
                    var bef = new JObject();
                    var aft = new JObject();

                    foreach (var prop in properties)
                    {
                        if (prop.IsModified)
                        {
                            if (prop.OriginalValue != null)
                            {
                                if (prop.OriginalValue != prop.CurrentValue)
                                {
                                    bef[prop.Metadata.Name] = JToken.FromObject(prop.OriginalValue, jsonSerializer);
                                }
                                else
                                {
                                    var originalValue = entry.GetDatabaseValues().GetValue<object>(prop.Metadata.Name);
                                    bef[prop.Metadata.Name] = originalValue != null
                                        ? JToken.FromObject(originalValue, jsonSerializer)
                                        : JValue.CreateNull();
                                }
                            }
                            else
                            {
                                bef[prop.Metadata.Name] = JValue.CreateNull();
                            }

                            aft[prop.Metadata.Name] = prop.CurrentValue != null
                            ? JToken.FromObject(prop.CurrentValue, jsonSerializer)
                            : JValue.CreateNull();
                        }
                    }

                    json["before"] = bef;
                    json["after"] = aft;

                    history.RowId = entry.PrimaryKey();
                    history.Kind = EntityState.Modified;
                    history.Changed = json.ToString(formatting);
                    break;
                case EntityState.Deleted:
                    foreach (var prop in properties)
                    {
                        json[prop.Metadata.Name] = prop.OriginalValue != null
                            ? JToken.FromObject(prop.OriginalValue, jsonSerializer)
                            : JValue.CreateNull();
                    }
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
