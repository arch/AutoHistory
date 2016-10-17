// Copyright (c) love.net team. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Newtonsoft.Json.Linq;

namespace Microsoft.EntityFrameworkCore {
    /// <summary>
    /// Represents a plugin for Microsoft.EntityFrameworkCore to support automatically recording data changes history.
    /// </summary>
    public static class DbContextAutoHistoryExtensions {
        /// <summary>
        /// Ensures the automatic history.
        /// </summary>
        /// <param name="context">The context.</param>
        public static void EnsureAutoHistory(this DbContext context) {
            // Must ToArray() here for excluding the AutoHistory model.
            var entries = context.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged && e.State != EntityState.Detached).ToArray();
            foreach (var entry in entries) {
                context.Add(entry.AutoHistory());
            }
        }

        internal static AutoHistory AutoHistory(this EntityEntry entry) {
            // TODO: get the really mapped table name.
            var history = new AutoHistory {
                TypeName = entry.Entity.GetType().FullName,
            };

            // Get the mapped properties for the entity type.
            // (include shadow properties, not include navigations & references)
            var properties = entry.Properties;

            var json = new JObject();
            switch (entry.State) {
                case EntityState.Added:
                    foreach (var prop in properties) {
                        //json[prop.Metadata.Name] = new JObject(prop.CurrentValue);
                        json[prop.Metadata.Name] = JToken.FromObject(prop.CurrentValue);
                    }

                    // REVIEW: what's the best way to set the RowId?
                    history.RowId = "0";
                    history.Kind = EntityState.Added;
                    history.Changed = json.ToString();
                    break;
                case EntityState.Modified:
                    var bef = new JObject();
                    var aft = new JObject();

                    foreach (var prop in properties) {
                        if (prop.IsModified) {
                            bef[prop.Metadata.Name] = JToken.FromObject(prop.OriginalValue);
                            aft[prop.Metadata.Name] = JToken.FromObject(prop.CurrentValue);
                        }
                    }

                    json["Before"] = bef;
                    json["After"]  = aft;

                    history.RowId = entry.PrimaryKey();
                    history.Kind = EntityState.Modified;
                    history.Changed = json.ToString();
                    break;
                case EntityState.Deleted:
                    foreach (var prop in properties) {
                        json[prop.Metadata.Name] = JToken.FromObject(prop.OriginalValue);
                    }
                    history.RowId = entry.PrimaryKey();
                    history.Kind = EntityState.Deleted;
                    history.Changed = json.ToString();
                    break;
                case EntityState.Detached:
                case EntityState.Unchanged:
                default:
                    throw new NotSupportedException("AutoHistory only support Added, Deleted and Modified entity.");
            }

            return history;
        }

        private static string PrimaryKey(this EntityEntry entry) {
            var key = entry.Metadata.FindPrimaryKey();

            var values = new List<object>();
            foreach (var property in key.Properties) {
                var value = entry.Property(property.Name).CurrentValue;
                if (value != null) {
                    values.Add(value);
                }
            }

            return string.Join(",", values);
        }
    }
}
