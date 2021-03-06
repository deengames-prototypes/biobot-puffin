using System;
using System.Collections.Generic;
using System.Linq;
using DeenGames.BioBot.Ecs.Components;
using DeenGames.BioBot.Ecs.Entities;
using DeenGames.BioBot.Events;
using GoRogue.MapGeneration;
using GoRogue.MapViews;
using Puffin.Core.Events;
using Troschuetz.Random;
using Troschuetz.Random.Generators;

namespace DeenGames.BioBot.Model
{
    // Your typical core-game map. It's a "floor" number of a specific biome, probably.
    public class AreaMap
    {
        // TODO: refactor into player
        internal List<BioBotEntity> Monsters = new List<BioBotEntity>();
        internal readonly BioBotEntity Player;

        private const int currentDifficutly = 1000;
        private readonly ArrayMap<bool> isWalkable;
        private readonly IGenerator globalRandom;
        // In TILES
        private readonly int width = 0;
        private readonly int height = 0;
        
        public AreaMap(IGenerator globalRandom)
        {
            this.globalRandom = globalRandom;
            this.width = Constants.MAP_TILES_WIDE;
            this.height = Constants.MAP_TILES_HIGH;
            this.isWalkable = new ArrayMap<bool>(Constants.MAP_TILES_WIDE, Constants.MAP_TILES_HIGH);

            this.Player = new BioBotEntity("Player", 0, 0).Add(new HealthComponent(500)).Add(new FightComponent(50, 15));

            // Each method gets its own RNG, so hopefully things are more segregated (less cascading changes)
            this.GenerateMap(new StandardGenerator(globalRandom.Next()));
            // TODO: more sophisticated.
            Player.X = this.width / 4;
            Player.Y =  this.height / 4;
            this.GenerateMonsters(new StandardGenerator(globalRandom.Next()));

            EventBus.LatestInstance.Subscribe(Signal.EntityDied, (obj) =>
            {
                var entity = (BioBotEntity)obj;
                if (Monsters.Contains(entity))
                {
                    this.Monsters.Remove(entity);
                }
            });
        }

        public bool this[int x, int y]
        {
            get {
                return this.isWalkable[x, y];
            }
        }

        public void TryToMove(BioBotEntity entity, int deltaX, int deltaY)
        {
            var destinationX = entity.X + deltaX;
            var destinationY = entity.Y + deltaY;

            if (destinationX < 0 || destinationX >= this.width || destinationY < 0 || destinationY >= this.height ||
                !this.isWalkable[destinationX, destinationY])
            {
                return;
            }
            else if (this.Monsters.Any(m => m.X == destinationX && m.Y == destinationY))
            {
                // NB: monsters will fight each other to get to you :/
                var target = this.Monsters.Single(m => m.X == destinationX && m.Y == destinationY);
                target.Add(new DamageComponent(entity.Get<FightComponent>().Strength - target.Get<FightComponent>().Toughness));
            }
            else if (this.Player.X == destinationX && this.Player.Y == destinationY)
            {
                Player.Add(new DamageComponent(entity.Get<FightComponent>().Strength - Player.Get<FightComponent>().Toughness));
            }
            else
            {
                // Clear, so move.
                entity.X = destinationX;
                entity.Y = destinationY;
            }
        }

        private void GenerateMap(IGenerator random)
        {
            QuickGenerators.GenerateRectangleMap(isWalkable);
            
            // Generate the exit. It's one side of the map where you can walk off.
            
            // TODO: don't collide with the entrance; we probably don't want to be on the same side,
            // and we definitely don't want to be too close (in terms of walking distance) from the
            // entrance to the exit. It should be a suitably long walk.

            // Pick a random side
            var sides = new string[] { "up", "down", "left", "right" };
            var side = sides[random.Next(sides.Length)];
            var exitSize = 3;

            if (side == "up" || side == "down")
            {
                var startX = exitSize + random.Next(this.width - (2 * exitSize));
                var y = side == "up" ? 0 : this.height - 1;
                for (var x = startX; x < startX + exitSize; x++)
                {
                    this.isWalkable[x, y] = true;
                }
            }
            else
            {
                var startY = exitSize + random.Next(this.height - (2 * exitSize));
                var x = side == "left" ? 0 : this.width - 1;
                for (var y = startY; y < startY + exitSize; y++)
                {
                    this.isWalkable[x, y] = true;
                }
            }
        }

        public void GenerateMonsters(IGenerator random)
        {
            // TODO: more sophisticated.
            var numMonsters = random.Next(6, 10);
            while (numMonsters-- > 0)
            {
                (var x, var y) = (random.Next(this.width), random.Next(this.height));
                
                while (!isWalkable[x, y] || Monsters.Any(m => m.X == x && m.Y == y) || (x == this.Player.X && y == this.Player.Y))
                {
                    (x, y) = (random.Next(this.width), random.Next(this.height));
                }

                var monster = new BioBotEntity("Slime", x, y)
                    .Add(new HealthComponent(100))
                    .Add(new FightComponent(25, 15))
                    .Add(new MovementBehaviourComponent(5, IdleBehaviour.NaiveStalk, SeenPlayerBehaviour.NaiveStalk));

                this.Monsters.Add(monster);
            }
        }
    }
}