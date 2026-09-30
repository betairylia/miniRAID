using System.Collections.Generic;
using UnityEngine;
namespace miniRAID.AlphaWolfPlaytest
{
    public static class AlphaWolfGeometry
    {
        // Supercover line: never jump diagonally through a corner blocker.
        public static List<Vector3Int> ChargeLine(Vector3Int from, Vector3Int to)
        {
            var cells = new List<Vector3Int>();
            int dx=to.x-from.x, dz=to.z-from.z, nx=Mathf.Abs(dx), nz=Mathf.Abs(dz);
            int sx=System.Math.Sign(dx), sz=System.Math.Sign(dz), ix=0, iz=0;
            var p=from;
            while(ix<nx || iz<nz)
            {
                if ((1+2*ix)*nz <= (1+2*iz)*nx) { p.x+=sx; ix++; }
                else { p.z+=sz; iz++; }
                cells.Add(p);
            }
            return cells;
        }
        public static HashSet<Vector3Int> Sweep(Vector3Int origin, Vector3Int target)
        {
            var d=target-origin;
            var forward=Mathf.Abs(d.x)>=Mathf.Abs(d.z)
                ? new Vector3Int(d.x>=0?1:-1,0,0) : new Vector3Int(0,0,d.z>=0?1:-1);
            var side=new Vector3Int(-forward.z,0,forward.x);
            var cells=new HashSet<Vector3Int>();
            for(int depth=1;depth<=3;depth++) for(int width=-1;width<=1;width++)
                cells.Add(origin+forward*depth+side*width);
            return cells;
        }
    }
}
