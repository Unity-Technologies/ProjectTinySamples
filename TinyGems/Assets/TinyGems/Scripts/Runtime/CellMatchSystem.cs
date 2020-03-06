using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.TinyGems
{
    public class CellMatchSystem : ComponentSystem
    {
        private bool m_SecondMatch;
        private int m_MatchCount;
        private Random m_Random;
        
        protected override void OnCreate()
        {
            base.OnCreate();
            
            RequireSingletonForUpdate<GameStateData>();
            RequireSingletonForUpdate<CellManager>();
            
            m_Random = new Random(31415);
        }
        
        void MakeOneMatch(NativeArray<CellInfo> cells, int startIndex, int end)
        {
            while (startIndex < end)
            {
                m_MatchCount++;
                PostUpdateCommands.DestroyEntity(cells[startIndex++].Entity);
            }
        }
        
        int FindOneMatch(NativeArray<CellInfo> cells, int startIndex)
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
        
        void FindMatches(ref NativeArray<CellInfo> cells)
        {
            for (var i = 0; i < cells.Length;)
                i = FindOneMatch(cells, i);
        }
        
        protected override void OnUpdate()
        {
            var gameState = GetSingleton<GameStateData>();
            if (gameState.State != GameState.Match)
                return;
            
            var cellMan = GetSingleton<CellManager>();
            m_MatchCount = 0;
            
            // find the match
            for (var x = 0; x < cellMan.MaxCol; x++)
            {
                var cells = Entities.GetCellEntitiesAtColumn(x);
                cells.Sort(new ByCol());
                FindMatches(ref cells);
            }
            
            for (var y = 0; y < cellMan.MaxRow; y++)
            {
                var cells = Entities.GetCellEntitiesAtRow(y);
                cells.Sort(new ByRow());
                FindMatches(ref cells);
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