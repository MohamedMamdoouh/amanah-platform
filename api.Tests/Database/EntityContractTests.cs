using System.Reflection;
using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Database;

public class EntityContractTests
{
    [Fact]
    public void All_db_set_entity_types_implement_IEntity()
    {
        var dbSetTypes = typeof(AppDbContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property =>
                property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(property => property.PropertyType.GetGenericArguments()[0])
            .ToList();

        var missing = dbSetTypes
            .Where(type => !typeof(IEntity).IsAssignableFrom(type))
            .Select(type => type.Name)
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void All_entity_classes_in_entities_folder_implement_IEntity()
    {
        var entityTypes = typeof(IEntity).Assembly
            .GetTypes()
            .Where(type =>
                type.IsClass
                && !type.IsAbstract
                && type.Namespace == typeof(IEntity).Namespace
                && type != typeof(IEntity)
                && type.GetProperty(nameof(IEntity.Id))?.PropertyType == typeof(Guid))
            .ToList();

        var missing = entityTypes
            .Where(type => !typeof(IEntity).IsAssignableFrom(type))
            .Select(type => type.Name)
            .ToList();

        Assert.Empty(missing);
    }
}
