using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.TinyGems
{
    public class CellMatchSystem : SystemBase
    {
        private bool m_SecondMatch;
        private int m_MatchCount;
        private Random m_Random;
        private EntityCommandBuffer m_CmdBuffer;
        
        protected override void OnCreate()
        {
            base.OnCreate();
            
            RequireSingletonForUpdate<GameStateData>();
            RequireSingletonForUpdate<CellManager>();
            
            m_Random = new Random(31415);
        }

        private void MakeOneMatch(in NativeArray<CellInfo> cells, int startIndex, int end)
        {
            while (startIndex < end)
            {
                m_MatchCount++;
                m_CmdBuffer.DestroyEntity(cells[startIndex++].Entity);
            }
        }
        
        private int FindOneMatch(in NativeArray<CellInfo> cells, int startIndex)
        {
            var matchCount = 0;
            var i = startIndex;
            var currentColor = cells[i].Cell.Color;
            
            for (;i < cells.Length; i++, matchCount++) 
            {
                if (cells[i].Cell.Color != currentColor)
                    break;
            }

            if (matchCount >= 3)
                MakeOneMatch(cells, startIndex, i);

            return i;
        }
        
        private void FindMatches(in NativeArray<CellInfo> cells)
        {
            for (var i = 0; i < cells.Length;)
                i = FindOneMatch(cells, i);
        }
        
        protected override void OnUpdate()
        {
            var gameState = GetSingleton<GameStateData>();
            if (gameState.State != GameState.Match)
                return;

            m_CmdBuffer = new EntityCommandBuffer(Allocator.TempJob);
            var cellMan = GetSingleton<CellManager>();
            m_MatchCount = 0;
            
            // find the match
            for (var x = 0; x < cellMan.MaxCol; x++)
            {
                var jobResult = new NativeList<CellInfo>(Allocator.Temp);
                Entities.ForEach((
                    in Entity entity, 
                    in Cell cell) =>
                {
                    CellUtil.AddCellAtColumn(entity, cell, x, ref jobResult);
                }).Run();
                
                var cells = jobResult.AsArray();
                cells.Sort(new ByCol());
                FindMatches(cells);  
            }
            
            for (var y = 0; y < cellMan.MaxRow; y++)
            {
                var jobResult = new NativeList<CellInfo>(Allocator.Temp);
                Entities.ForEach((
                    in Entity entity, 
                    in Cell cell) =>
                {
                    CellUtil.AddCellAtRow(entity, cell, y, ref jobResult);
                }).Run();
                
                var cells = jobResult.AsArray();
                cells.Sort(new ByRow());
                FindMatches(cells);
            }

            if (m_MatchCount == 0)
            {
                if (!m_SecondMatch)
                {
                    gameState.Hearts--;

                    if (gameState.Hearts == 0)
                    {
                        AudioUtils.PlaySound(EntityManager, AudioTypes.GameOver);
                        gameState.State = GameState.GameOver;
                    }
                    else
                    {
                        AudioUtils.PlaySound(EntityManager, GetFailSounds());
                        gameState.State = GameState.Swap;                        
                    }
                }
                else
                {
                    gameState.State = GameState.Swap;
                }
                m_SecondMatch = false;
            }
            else
            {
                AudioUtils.PlaySound(EntityManager, GetMatchSound());
                
                gameState.State = GameState.Spawn;
                m_SecondMatch = true;
            }
            
            SetSingleton(gameState);
            m_CmdBuffer.Playback(EntityManager);
            m_CmdBuffer.Dispose();
        }

        private AudioTypes GetMatchSound()
        {
            var randomSfx = m_Random.NextInt(0, 4);
            AudioTypes matchSound = AudioTypes.None;
            switch (randomSfx)
            {
                case 0:
                    matchSound = AudioTypes.Match0;
                    break;
                case 1:
                    matchSound = AudioTypes.Match1;
                    break;
                case 2:
                    matchSound = AudioTypes.Match2;
                    break;
                case 3:
                    matchSound = AudioTypes.Match3;
                    break;
            }

            return matchSound;
        }
        
        private AudioTypes GetFailSounds()
        {
            var randomSfx = m_Random.NextInt(0, 4);
            AudioTypes matchSound = AudioTypes.None;
            switch (randomSfx)
            {
                case 0:
                    matchSound = AudioTypes.Fail0;
                    break;
                case 1:
                    matchSound = AudioTypes.Fail1;
                    break;
                case 2:
                    matchSound = AudioTypes.Fail2;
                    break;
                case 3:
                    matchSound = AudioTypes.Fail3;
                    break;
            }

            return matchSound;
        }        
    }
}