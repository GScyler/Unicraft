namespace MinecraftEngine
{
    public static class VoxelSettings
    {
        // В 1.18 высота чанка увеличена до 384 блоков
        public const int ChunkWidth = 16;
        public const int ChunkHeight = 384; 
        public const int ChunkDepth = 16;
        
        // В массиве индексы идут от 0 до 384. 
        // Индекс Y = 0 в массиве соответствует Y = -64 в игровом мире.
        public const int WorldYOffset = 64; 
        
        // Уровень моря (в игровых координатах Y = 63, в массиве Y = 127)
        public const int SeaLevel = 63;

        public const int ChunkVolume = ChunkWidth * ChunkHeight * ChunkDepth;
    }
}