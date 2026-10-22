
using Godot;
using RSF.Core.Blueprints;
using RSF.Core.Components;
using RSF.Core.Entities;
using RSF.Core.Simulation;
using RSF.Core.Systems;
using RSF.Core.Commands;
using RSF.Core.CommandHandlers;
using System.Collections.Generic;

public partial class Main : Node2D
{
	private World _world = null!;
	private Entity _player;
	private MovementSystem _movementSystem = null!;

	private const float CellSize = 32.0f;
	private const float MoveSpeed = 5.0f;
	
	private const int MapWidth = 30;
	private const int MapHeight = 20;
	
	private readonly List<Entity> _groundItems = new();
	private WorldClock _clock = null!;
	private InventorySystem _inventorySystem = null!;
	private PickupCommandHandler _pickupHandler = null!;
	private UseItemCommandHandler _useItemHandler = null!;

	private bool _wasEPressed;
	private bool _wasFPressed;
		
	public override void _Ready()
	{
		_world = new World();
		_movementSystem = new MovementSystem(_world);
		
		_clock = new WorldClock();

		_inventorySystem = new InventorySystem(_world);

		ItemEffectSystem itemEffectSystem = new(
			_world,
			_clock);

		_pickupHandler = new PickupCommandHandler(
			_world,
			_inventorySystem);

		_useItemHandler = new UseItemCommandHandler(
			_world,
			_inventorySystem,
			itemEffectSystem);

		AppleBlueprint appleBlueprint = new();
		SoupBlueprint soupBlueprint = new();

		_groundItems.Add(appleBlueprint.Spawn(_world, 8, 5));
		_groundItems.Add(appleBlueprint.Spawn(_world, 12, 8));
		_groundItems.Add(appleBlueprint.Spawn(_world, 6, 10));

		_groundItems.Add(soupBlueprint.Spawn(_world, 15, 5));
		_groundItems.Add(soupBlueprint.Spawn(_world, 18, 9));
		
		HumanBlueprint blueprint = new();
		_player = blueprint.Spawn(_world, "Arthas", 5, 5);

		GD.Print("Player spawned: Arthas");
	}

	public override void _Process(double delta)
	{
		Vector2 direction = Input.GetVector(
			"ui_left",
			"ui_right",
			"ui_up",
            "ui_down"
		);

		_movementSystem.SetVelocity(
			_player,
			(int)Mathf.Sign(direction.X),
			(int)Mathf.Sign(direction.Y)
		);

		_movementSystem.Update((float)delta * MoveSpeed);
		if (_world.TryGet<PositionComponent>(_player, out var position))
		{
			float maxX = MapWidth - 1;
			float maxY = MapHeight - 1;

			float x = Mathf.Clamp(position.X, 0, maxX);
			float y = Mathf.Clamp(position.Y, 0, maxY);

			_world.Set(_player, new PositionComponent(x, y));
		}
		QueueRedraw();
		
		bool ePressed = Input.IsKeyPressed(Key.E);
		bool fPressed = Input.IsKeyPressed(Key.F);

		// E: pick up a nearby ground item.
		if (ePressed && !_wasEPressed)
		{
			if (_world.TryGet<PositionComponent>(
				_player, out var playerPosition))
			{
				Vector2 playerPos = new(
					playerPosition.X,
					playerPosition.Y);

				for (int i = _groundItems.Count - 1; i >= 0; i--)
				{
					Entity item = _groundItems[i];

					if (!_world.TryGet<PositionComponent>(
						item, out var itemPosition))
						continue;

					Vector2 itemPos = new(
						itemPosition.X,
						itemPosition.Y);

					if ((playerPos - itemPos).Length() > 1.5f)
						continue;

					_pickupHandler.Handle(
						new PickupCommand(_player, item));

					if (_inventorySystem.HasItem(_player, item))
					{
						_groundItems.RemoveAt(i);
						break;
					}
				}
			}
		}

		// F: consume the first item in the inventory.
		if (fPressed && !_wasFPressed)
		{
			if (_world.TryGet<InventoryComponent>(
				_player, out var inventory))
			{
				if (inventory.Items.Count > 0)
				{
					Entity item = inventory.Items[0];

					_useItemHandler.Handle(
						new UseItemCommand(_player, item));
				}
			}
		}

		_wasEPressed = ePressed;
		_wasFPressed = fPressed;

	}

	public override void _Draw()
	{
		DrawMap();
		if (!_world.TryGet<PositionComponent>(_player, out var position))
			return;

		
		foreach (Entity item in _groundItems)
		{
			if (!_world.TryGet<PositionComponent>(
				item, out var itemPosition))
				continue;

			if (!_world.TryGet<ItemComponent>(
				item, out var itemData))
				continue;

			Vector2 itemScreenPosition = new(
				itemPosition.X * CellSize,
				itemPosition.Y * CellSize);

			bool isApple = itemData.Name == "Apple";

			Color itemColor = isApple
				? Colors.Red
				: Colors.Orange;

			DrawCircle(
				itemScreenPosition,
				10.0f,
				itemColor);

			DrawString(
				ThemeDB.FallbackFont,
				itemScreenPosition + new Vector2(-18, -14),
				itemData.Name,
				fontSize: 14,
				modulate: Colors.White);
		}


		// Draw the player.
		Vector2 screenPosition = new(
			position.X * CellSize,
			position.Y * CellSize
		);

		DrawCircle(screenPosition, 12.0f, Colors.CornflowerBlue);

		DrawString(
			ThemeDB.FallbackFont,
			screenPosition + new Vector2(-24, -18),
			"Arthas",
			fontSize: 14,
			modulate: Colors.White
		);

		// Draw the character status panel.
		DrawRect(
			new Rect2(10, 10, 300, 210),
			new Color(0.08f, 0.08f, 0.12f, 0.92f)
		);

		float textX = 22;
		float textY = 34;
		float lineHeight = 25;

		DrawString(
			ThemeDB.FallbackFont,
			new Vector2(textX, textY),
			"CHARACTER STATUS",
			fontSize: 18,
			modulate: Colors.Gold
		);

		textY += lineHeight + 3;

		if (_world.TryGet<HealthComponent>(_player, out var health))
		{
			DrawString(
				ThemeDB.FallbackFont,
				new Vector2(textX, textY),
				$"HP: {health.Current} / {health.Max}",
				fontSize: 16,
				modulate: Colors.LightGreen
			);
		}

		textY += lineHeight;

		if (_world.TryGet<StrengthComponent>(_player, out var strength))
		{
			DrawString(
				ThemeDB.FallbackFont,
				new Vector2(textX, textY),
				$"Strength: {strength.Value}",
				fontSize: 16
			);
		}

		textY += lineHeight;

		if (_world.TryGet<VitalityComponent>(_player, out var vitality))
		{
			DrawString(
				ThemeDB.FallbackFont,
				new Vector2(textX, textY),
				$"Vitality: {vitality.Value}",
				fontSize: 16
			);
		}

		textY += lineHeight;

		if (_world.TryGet<HungerComponent>(_player, out var hunger))
		{
			DrawString(
				ThemeDB.FallbackFont,
				new Vector2(textX, textY),
				$"Hunger: {hunger.Current} / {hunger.Max}",
				fontSize: 16,
				modulate: Colors.Orange
			);
		}
		
		string inventoryText = "Inventory: ";

		if (_world.TryGet<InventoryComponent>(
			_player, out var inventory))
		{
			if (inventory.Items.Count == 0)
			{
				inventoryText += "(empty)";
			}
			else
			{
				List<string> itemNames = new();

				foreach (Entity item in inventory.Items)
				{
					if (_world.TryGet<ItemComponent>(
						item, out var itemData))
					{
						itemNames.Add(itemData.Name);
					}
				}

				inventoryText += string.Join(", ", itemNames);
			}
		}
		
		textY += lineHeight;

		DrawString(
			ThemeDB.FallbackFont,
			new Vector2(textX, textY),
			inventoryText,
			fontSize: 16,
			modulate: Colors.White
		);
	}
	
	private void DrawMap()
	{
		for (int x = 0; x < MapWidth; x++)
		{
			for (int y = 0; y < MapHeight; y++)
			{
				Rect2 tile = new(
					x * CellSize,
					y * CellSize,
					CellSize,
					CellSize
				);

				DrawRect(tile, new Color(0.12f, 0.20f, 0.14f));
				DrawRect(tile, new Color(0.25f, 0.32f, 0.25f), false, 1.0f);
			}
		}
	}
}
