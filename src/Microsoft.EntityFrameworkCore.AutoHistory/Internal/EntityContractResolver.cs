// Copyright (c) Arch team. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Microsoft.EntityFrameworkCore.Internal {
    internal class EntityContractResolver : DefaultContractResolver {
        private readonly DbContext _dbContext;

        public EntityContractResolver(DbContext dbContext) {
            _dbContext = dbContext;
        }

        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization) {
            var list = base.CreateProperties(type, memberSerialization);

            var entry = _dbContext.ChangeTracker.Entries().FirstOrDefault(e => e.Entity.GetType() == type);
            if (entry == null) {
                return list;
            }

            // Get the navigations
            var navigations = entry.Metadata.GetNavigations().Select(n => n.Name);

            // Exclude the navigation properties
            return list.Where(p => !navigations.Contains(p.PropertyName)).ToArray();
        }
    }
}
