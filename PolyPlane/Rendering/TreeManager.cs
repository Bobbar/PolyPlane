using PolyPlane.Helpers;
using unvell.D2DLib;

namespace PolyPlane.Rendering
{
    public sealed class TreeManager : IDisposable
    {
        private List<Tree> _trees = new List<Tree>();
        private float[] _treePosIdx = Array.Empty<float>();

        public void Render(RenderContext ctx)
        {
            const int IDX_PAD = 2;
            var todColor = ctx.GetTimeOfDayColor();
            var shadowColor = ctx.GetShadowColor();
            var shadowAngle = Tree.GetTreeShadowAngle();

            // Find the indices for the trees closest to the left and right sides of the viewport.
            var leftIdx = Math.Clamp(ClosestTreeIdx(ctx.Viewport.left) - IDX_PAD, 0, _treePosIdx.Length);
            var rightIdx = Math.Clamp(ClosestTreeIdx(ctx.Viewport.right) + IDX_PAD, 0, _treePosIdx.Length);

            for (int i = leftIdx; i < rightIdx; i++)
            {
                var tree = _trees[i];

                if (ctx.Viewport.Contains(tree.Position, tree.TotalHeight * Tree.TREE_SCALE))
                {
                    tree.Render(ctx, todColor, shadowColor, shadowAngle);
                }
            }
        }

        public void GenTrees(Random rnd, int num, RenderContext ctx)
        {
            // Gen trees.
            var treeDeDup = new HashSet<D2DPoint>();

            var trunkColorNormal = D2DColor.Chocolate;
            var trunkColorNormalDark = new D2DColor(1f, 0.29f, 0.18f, 0.105f);
            var leafColorNormal = D2DColor.ForestGreen;
            var trunkColorPine = D2DColor.BurlyWood;
            var leafColorPine = D2DColor.Green;
            var minDist = rnd.NextFloat(20f, 200f);
            var fieldRange = World.FieldXBounds;

            for (int i = 0; i < num; i++)
            {
                var rndPos = new D2DPoint(rnd.NextFloat(-fieldRange, fieldRange), 0f);

                while (!treeDeDup.Add(rndPos) || (_trees.Count > 0 && _trees.Min(t => t.Position.DistanceTo(rndPos)) < minDist))
                    rndPos = new D2DPoint(rnd.NextFloat(-fieldRange, fieldRange), 0f);

                var type = rnd.Next(10);
                var height = (int)(10f + (rnd.NextFloat(1f, 3f) * 20f));

                Tree newTree;

                if (type <= 8)
                {
                    var radius = rnd.Next(40, 80);

                    var leafColor = leafColorNormal;
                    leafColor.g -= rnd.NextFloat(0.0f, 0.2f);

                    var trunkColor = Utilities.LerpColor(trunkColorNormal, trunkColorNormalDark, rnd.NextFloat(0f, 1f));
                    var trunkWidth = rnd.NextFloat(3f, 7f);

                    newTree = new NormalTree(ctx, rndPos, height, radius, trunkWidth, trunkColor, leafColor);
                }
                else
                {
                    var width = rnd.NextFloat(20f, 30f);
                    newTree = new PineTree(ctx, rndPos, height, width, trunkColorPine, leafColorPine);
                }

                _trees.Add(newTree);

                if (i % 50 == 0)
                    minDist = rnd.NextFloat(20f, 200f);
            }

            // Sort the trees by X position.
            _trees = _trees.OrderBy(t => t.Position.X).ToList();

            // Build and index containing the X positions of each tree.
            // Used for efficient viewport clipping.
            var treeIdx = new List<float>();
            _trees.ForEach(t =>
            {
                treeIdx.Add(t.Position.X);
            });

            _treePosIdx = treeIdx.ToArray();

        }

        /// <summary>
        /// Finds the index of the closest tree using a binary search strategy.
        /// </summary>
        private int ClosestTreeIdx(float target)
        {
            float res = _treePosIdx[0];
            int resIdx = 0;
            int lo = 0, hi = _treePosIdx.Length - 1;

            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;

                // Update res if mid is closer to target
                if (Math.Abs(_treePosIdx[mid] - target) < Math.Abs(res - target))
                {
                    res = _treePosIdx[mid];
                    resIdx = mid;


                }
                // In case of a tie, prefer larger value
                else if (Math.Abs(_treePosIdx[mid] - target) == Math.Abs(res - target))
                {
                    if (_treePosIdx[mid] > res)
                        resIdx = mid;

                    res = Math.Max(res, _treePosIdx[mid]);

                }

                if (_treePosIdx[mid] == target)
                {
                    return mid;
                }
                else if (_treePosIdx[mid] < target)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return resIdx;
        }

        public void Dispose()
        {
            _trees.ForEach(t => t.Dispose());
            _treePosIdx = Array.Empty<float>();
        }
    }
}
