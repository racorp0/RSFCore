using RSF.Core.Common;
namespace RSF.Core.Components;

/// <summary>Stores an NPC's explicit job; replacing it updates schedule selection.</summary>
/// <param name="Job">The current job, or None when unemployed.</param>
public readonly record struct JobComponent(Job Job) : IComponent;
