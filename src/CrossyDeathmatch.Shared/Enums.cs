namespace CrossyDeathmatch.Shared;

public enum LaneType { Grass, Road, River, Rail }

public enum Direction { Up, Down, Left, Right }

public enum PlayerRole { Runner, Mastermind }

public enum RoomPhase { Lobby, CharacterSelect, Playing, Results }

public enum CharacterKind { Chicken, Rabbit, Duck, Pig }

public enum AbilityKind { Car, Boulder, Fox, Rain, Night }

public enum AbilityTarget { None, Lane, Tile }

public enum ObstacleKind { Car, Truck, Log, Train, Fox, Boulder }

public enum PickupKind { Coin, Speed, Life }

public enum RunnerState { Alive, Respawning, Out }

public enum MessageTone { Info, Good, Bad }
