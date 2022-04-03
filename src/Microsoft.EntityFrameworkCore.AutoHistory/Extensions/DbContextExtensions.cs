// Copyright (c) Arch team. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Internal;

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
            var entries = context.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted)
                .ToArray();
            foreach (var entry in entries)
            {
                var autoHistory = entry.AutoHistory(createHistoryFactory);
                if (autoHistory != null)
                {
                    context.Add<TAutoHistory>(autoHistory);
                }
            }
        }

        internal static TAutoHistory AutoHistory<TAutoHistory>(this EntityEntry entry, Func<TAutoHistory> createHistoryFactory)
            where TAutoHistory : AutoHistory
        {
            if (IsEntityExcluded(entry))
            {
                return null;
            }

            var properties = GetPropertiesWithoutExcluded(entry);
            if (!(properties.Any(p => p.IsModified) || entry.State == EntityState.Deleted))
            {
                return null;
            }

            var history = createHistoryFactory();
            history.TableName = entry.Metadata.GetTableName();
            switch (entry.State)
            {
                case EntityState.Added:
                    WriteHistoryAddedState(history, properties);
                    break;
                case EntityState.Modified:
                    WriteHistoryModifiedState(history, entry, properties);
                    break;
                case EntityState.Deleted:
                    WriteHistoryDeletedState(history, entry, properties);
                    break;
                case EntityState.Detached:
                case EntityState.Unchanged:
                default:
                    throw new NotSupportedException("AutoHistory only support Deleted and Modified entity.");
            }

            return history;
        }

        private static bool IsEntityExcluded(EntityEntry entry) =>
            entry.Metadata.ClrType.GetCustomAttributes(typeof(ExcludeFromHistoryAttribute), true).Any();

        private static IEnumerable<PropertyEntry> GetPropertiesWithoutExcluded(EntityEntry entry)
        {
            // Get the mapped properties for the entity type.
            // (include shadow properties, not include navigations & references)
            var excludedProperties = entry.Metadata.ClrType.GetProperties()
                    .Where(p => p.GetCustomAttributes(typeof(ExcludeFromHistoryAttribute), true).Count() > 0)
                    .Select(p => p.Name);

            var properties = entry.Properties.Where(f => !excludedProperties.Contains(f.Metadata.Name));
            return properties;
        }
        
        /// <summary>
        /// Ensures the history for added entries
        /// </summary>
        /// <param name="context"></param>
        /// <param name="addedEntries"></param>
        public static void EnsureAddedHistory(
            this DbContext context,
            EntityEntry[] addedEntries)
        {
            EnsureAddedHistory<AutoHistory>(
                context,
                () => new AutoHistory(),
                addedEntries);
        }

        public static void EnsureAddedHistory<TAutoHistory>(
            this DbContext context,
            Func<TAutoHistory> createHistoryFactory,
            EntityEntry[] addedEntries)
            where TAutoHistory : AutoHistory
        {
            foreach (var entry in addedEntries)
            {
                var autoHistory = entry.AutoHistory(createHistoryFactory);
                if (autoHistory != null)
                {
                    context.Add<TAutoHistory>(autoHistory);
                }
            }
        }

        internal static TAutoHistory AddedHistory<TAutoHistory>(
            this EntityEntry entry,
            Func<TAutoHistory> createHistoryFactory)
            where TAutoHistory : AutoHistory
        {
            if (IsEntityExcluded(entry))
            {
                return null;
            }

            var properties = GetPropertiesWithoutExcluded(entry);

            dynamic json = new System.Dynamic.ExpandoObject();
            foreach (var prop in properties)
            {
                ((IDictionary<string, object>)json)[prop.Metadata.Name] = prop.OriginalValue != null ?
                                                                          prop.OriginalValue
                                                                          : null;
            }
            var history = createHistoryFactory();
            history.TableName = entry.Metadata.GetTableName();
            history.RowId = entry.PrimaryKey();
            history.Kind = EntityState.Added;
            history.Changed = JsonSerializer.Serialize(json, AutoHistoryOptions.Instance.JsonSerializerOptions);
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

        private static void WriteHistoryAddedState(AutoHistory history, IEnumerable<PropertyEntry> properties)
        {
            dynamic json = new System.Dynamic.ExpandoObject();

            foreach (var prop in properties)
            {
                if (prop.Metadata.IsKey() || prop.Metadata.IsForeignKey())
                {
                    continue;
                }
                ((IDictionary<String, Object>)json)[prop.Metadata.Name] = prop.CurrentValue;
            }

            // REVIEW: what's the best way to set the RowId?
            history.RowId = "0";
            history.Kind = EntityState.Added;
            history.Changed = JsonSerializer.Serialize(json);
        }

        private static void WriteHistoryModifiedState(AutoHistory history, EntityEntry entry, IEnumerable<PropertyEntry> properties)
        {
            dynamic json = new System.Dynamic.ExpandoObject();
            dynamic bef = new System.Dynamic.ExpandoObject();
            dynamic aft = new System.Dynamic.ExpandoObject();

            PropertyValues databaseValues = null;
            foreach (var prop in properties)
            {
                if (prop.IsModified)
                {
                    if (prop.OriginalValue != null)
                    {
                        if (!prop.OriginalValue.Equals(prop.CurrentValue))
                        {
                            ((IDictionary<String, Object>)bef)[prop.Metadata.Name] = prop.OriginalValue;
                        }
                        else
                        {
                            databaseValues ??= entry.GetDatabaseValues();
                            var originalValue = databaseValues.GetValue<object>(prop.Metadata.Name);
                            ((IDictionary<String, Object>)bef)[prop.Metadata.Name] = originalValue;
                        }
                    }
                    else
                    {
                        ((IDictionary<String, Object>)bef)[prop.Metadata.Name] = null;
                    }

                    ((IDictionary<String, Object>)aft)[prop.Metadata.Name] = prop.CurrentValue;
                }
            }

            ((IDictionary<String, Object>)json)["before"] = bef;
            ((IDictionary<String, Object>)json)["after"] = aft;

            history.RowId = entry.PrimaryKey();
            history.Kind = EntityState.Modified;
            history.Changed = JsonSerializer.Serialize(json, AutoHistoryOptions.Instance.JsonSerializerOptions);
        }

        private static void WriteHistoryDeletedState(AutoHistory history, EntityEntry entry, IEnumerable<PropertyEntry> properties)
        {
            dynamic json = new System.Dynamic.ExpandoObject();

            foreach (var prop in properties)
            {
                ((IDictionary<String, Object>)json)[prop.Metadata.Name] = prop.OriginalValue;
            }
            history.RowId = entry.PrimaryKey();
            history.Kind = EntityState.Deleted;
            history.Changed = JsonSerializer.Serialize(json, AutoHistoryOptions.Instance.JsonSerializerOptions);
        }
    }
}
