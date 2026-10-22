using RSF.Core.Simulation;
using System.Text;
using RSF.Core.Entities;

namespace RSF.Core.Debugging;

public static class WorldDebugger
{
    public static string DumpWorld(World world)
    {
        StringBuilder sb = new();

        sb.AppendLine("========== WORLD ==========");

        foreach (var entity in world.GetEntities())
        {
            sb.AppendLine($"Entity: {entity.Id}");
        }

        sb.AppendLine("===========================");

        return sb.ToString();
    }

    /// <summary>Describes an entity's components, including named inventory contents.</summary>
    /// <param name="world">The world containing the entity and its inventory items.</param>
    /// <param name="entity">The entity to inspect.</param>
    /// <returns>A readable description of the entity's current state.</returns>
    public static string DumpEntity(
    World world,
    Entity entity)
    {
        StringBuilder sb = new();

        sb.AppendLine("========== ENTITY ==========");
        sb.AppendLine($"Id: {entity.Id}");
        sb.AppendLine();

        foreach (object component in
         world.GetComponents(entity.Id))
        {
            sb.Append(
                ComponentDebugger.Dump(component, world));
        }

        sb.AppendLine("============================");

        return sb.ToString();
    }
}
