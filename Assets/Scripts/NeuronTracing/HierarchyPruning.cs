using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public class HierarchyPruning
{
    public class HierarchySegment
    {
        public HierarchySegment parent;
        public Marker leafMarker;
        public Marker rootMarker;
        public double length;
        public int level;

        public HierarchySegment()
        {
            leafMarker = null;
            rootMarker = null;
            length = 0;
            level = 1;
            parent = null;
        }

        public void SetValue(Marker _leaf, Marker _root, double _len, int _level)
        {
            leafMarker = _leaf;
            rootMarker = _root;
            length = _len;
            level = _level;
            parent = null;
        }

        public List<Marker> GetMarkers()
        {
            Marker p = leafMarker;
            List<Marker> markers = new();
            while (p != rootMarker)
            {
                markers.Add(p);
                p = p.parent;
            }
            markers.Add(rootMarker);
            return markers;
        }
    }

    List<HierarchySegment> SWC2TopoSegs(List<Marker> inswc, byte[] img, int sz0, int sz1, int sz2)
    {
        int tolNum = inswc.Count;
        Dictionary<Marker, int> swcMap = new(tolNum);
 
        for (int i = 0; i < tolNum; i++)
        {
            swcMap[inswc[i]] = i;
        }

        int[] childs_num = new int[tolNum];

        Parallel.ForEach(inswc, marker =>
        {
            if (marker.parent != null)
            {
                int parent_index = swcMap[marker.parent];
                Interlocked.Increment(ref childs_num[parent_index]);
            }
        });

        ConcurrentBag<Marker> leafMarkerBag = new ConcurrentBag<Marker>();

        Parallel.For(0, tolNum, i =>
        {
            if (childs_num[i] == 0)
            {
                leafMarkerBag.Add(inswc[i]);
            }
        });

        List<Marker> leafMarkers = leafMarkerBag.ToList();
        int leafNum = leafMarkers.Count;

        int sz01 = sz0 * sz1;

        double[] topoDists = new double[tolNum];

        Marker[] topoLeafs = new Marker[tolNum];

        //calculate distance
        for (int i = 0; i < leafNum; i++)
        {
            Marker leafMarker = leafMarkers[i];
            Marker childNode = leafMarkers[i];
            Marker parentNode = childNode.parent;
            int child_index = swcMap[childNode];
            topoLeafs[child_index] = leafMarker;
            topoDists[child_index] = img[leafMarker.img_index(sz0, sz01)] / 255.0;
            //topoDists[child_index] = 0;

            while (parentNode != null)
            {
                int parent_index = swcMap[parentNode];
                double tmp_dst = (img[parentNode.img_index(sz0, sz01)]) / 255.0 + topoDists[child_index];
                //double tmp_dst = Vector3.Distance(parentNode.position,childNode.position) + topoDists[child_index];
                if (tmp_dst > topoDists[parent_index])
                {
                    topoDists[parent_index] = tmp_dst;
                    topoLeafs[parent_index] = topoLeafs[child_index];
                }
                else break;
                child_index = parent_index;
                parentNode = parentNode.parent;
            }
        }

        //activate hierarchy segments
        Dictionary<Marker, int> leafMap = new(leafNum);
        List<HierarchySegment> topoSegs = new(leafNum);
        for (int i = 0; i < leafNum; i++)
        {
            topoSegs.Add(new HierarchySegment());
            leafMap[leafMarkers[i]] = i;
        }

        for (int i = 0; i < leafNum; i++)
        {
            Marker leafMarker = leafMarkers[i];
            Marker rootMarker = leafMarker;
            Marker rootParent = rootMarker.parent;
            int level = 1;
            while (rootParent != null && topoLeafs[swcMap[rootParent]] == leafMarker)
            {
                if (childs_num[swcMap[rootMarker]] >= 2) level++;
                rootMarker = rootParent;
                rootParent = rootMarker.parent;
            }

            double dst = topoDists[swcMap[rootMarker]];

            topoSegs[i].SetValue(leafMarker, rootMarker, dst, level);


            if (rootParent == null)
            {
                topoSegs[i].parent = null;
            }
            else
            {
                Marker leafMarker2 = topoLeafs[swcMap[rootParent]];
                if (leafMarker2 != null)
                {
                    int leafIndex2 = leafMap[leafMarker2];
                    topoSegs[i].parent = topoSegs[leafIndex2];
                }
            }
        }

        return topoSegs;
    }

    List<Marker> TopoSegs2swc(List<HierarchySegment> topoSegs, int swcType)
    {
        var outswc = new List<Marker>();
        double min_dst = double.MaxValue;
        double max_dst = double.MinValue;
        int min_level = int.MaxValue;
        int max_level = int.MinValue;
        foreach (HierarchySegment topo_seg in topoSegs)
        {
            double dst = topo_seg.length;
            min_dst = Math.Min(dst, min_dst);
            max_dst = Math.Max(dst, max_dst);
            int level = topo_seg.level;
            min_level = Math.Min(level, min_level);
            max_level = Math.Max(level, max_level);
        }

        max_level = Math.Min(max_level, 20);

        max_dst -= min_dst;
        if (max_dst == 0) max_dst = 0.0000001;
        max_level -= min_level;
        if (max_level == 0) max_level = 1;
        foreach (HierarchySegment topo_seg in topoSegs)
        {
            double dst = topo_seg.length;
            int level = Math.Min(topo_seg.level, max_level);

            int color_id = (int)((swcType == 0) ? (dst - min_dst) / max_dst * 254 + 20.5 : (level - min_level) / max_level * 254.0 + 20.5);
            List<Marker> tmp_markers;
            tmp_markers = topo_seg.GetMarkers();
            foreach (Marker marker in tmp_markers)
            {
                //marker.type = color_id;
            }
            outswc.AddRange(tmp_markers);
        }
        return outswc;
    }

    void topo_segs2swc(HashSet<HierarchySegment> out_segs, List<HierarchySegment> filtered_segs, out List<Marker> outswc, int swc_type)
    {
        outswc = new List<Marker>();
        foreach (HierarchySegment topo_seg in filtered_segs)
        {
            int color_id = out_segs.Contains(topo_seg) ? 0 : 1;
            List<Marker> tmp_markers;
            tmp_markers = topo_seg.GetMarkers();
            foreach (Marker marker in tmp_markers)
            {
                //marker.type = color_id;
            }
            outswc.AddRange(tmp_markers);
        }
    }

    public List<Marker> HierarchyPrune(List<Marker> inswc, byte[] img, int size0, int size1, int size2, ref float somaRadius, double bkg_thresh = 30.0, double SR_ratio = 1.0 / 9.0, float lengthFactor = 4, float lengthThreshold = 0.5f)
    {
        int size01 = size0 * size1;
        int totalSize = size01 * size2;

        List<HierarchySegment> topoSegs = SWC2TopoSegs(inswc, img, size0, size1, size2);

        List<HierarchySegment> filterSegs = new();
        Marker root = inswc.FirstOrDefault(marker => marker.parent == null);

        double realThresh = Math.Max(10, bkg_thresh);

        if (somaRadius < 0) somaRadius = root.MarkerRadius(img, size0, size1, size2, realThresh);
        Debug.Log($"Soma Radius: {somaRadius}");

        foreach (HierarchySegment topoSeg in topoSegs)
        {
            Marker leafMarker = topoSeg.leafMarker;
            if (Vector3.Distance(leafMarker.position, root.position) < 3 * somaRadius)
            {
                if (topoSeg.length >= somaRadius * lengthFactor)
                {
                    filterSegs.Add(topoSeg);
                }
            }
            else
            {
                if (topoSeg.length >= lengthThreshold)
                {
                    filterSegs.Add(topoSeg);
                }
            }
        }

        //calculate radius of every node
        List<Marker> markers = new();
        foreach (var seg in filterSegs)
        {
            Marker leafMarker = seg.leafMarker;
            Marker rootMarker = seg.rootMarker;
            Marker p = leafMarker;
            while (p != rootMarker.parent)
            {
                markers.Add(p);
                p = p.parent;
            }
        }

        Parallel.ForEach(markers, marker =>
        {
            marker.radius = marker.MarkerRadius(img, size0, size1, size2, realThresh);
            //marker.radius = 2.0f;
        });
        root.radius = somaRadius;

        //hierarchy pruning
        byte[] tmpimg = new byte[img.Length];
        img.CopyTo(tmpimg, 0);

        filterSegs.Sort((a, b) => -a.length.CompareTo(b.length));

        List<HierarchySegment> outSegs = new();
        double tolSumSig = 0.0, tolSumRdc = 0.0;
        HashSet<HierarchySegment> visitedSegs = new();

        foreach (var seg in filterSegs)
        {
            if (seg.parent != null && !visitedSegs.Contains(seg.parent)) continue;
            Marker leafMarker = seg.leafMarker;
            Marker rootMarker = seg.rootMarker;

            double sum_sig = 0;
            double sum_rdc = 0;

            Marker p = leafMarker;
            while (p != rootMarker.parent)
            {
                if (tmpimg[p.img_index(size0, size01)] == 0)
                {
                    sum_rdc += img[p.img_index(size0, size01)];
                }
                else
                {
                    int r = (int)p.radius;
                    int x = (int)(p.position.x);
                    int y = (int)(p.position.y);
                    int z = (int)(p.position.z);
                    double sum_sphere_size = 0;
                    double sum_delete_size = 0;
                    for (int ii = -r; ii <= r; ii++)
                    {
                        int x2 = x + ii;
                        if (x2 < 0 || x2 >= size0) continue;
                        for (int jj = -r; jj <= r; jj++)
                        {
                            int y2 = y + jj;
                            if (y2 < 0 || y2 >= size1) continue;
                            for (int kk = -r; kk <= r; kk++)
                            {
                                int z2 = z + kk;
                                if (z2 < 0 || z2 >= size2) continue;
                                if (ii * ii + jj * jj + kk * kk > r * r) continue;
                                int index = z2 * size01 + y2 * size0 + x2;
                                sum_sphere_size++;
                                if (tmpimg[index] != img[index])
                                {
                                    sum_delete_size++;
                                }
                            }
                        }
                    }

                    if (sum_sphere_size > 0 && sum_delete_size / sum_sphere_size > 0.1)
                    {
                        sum_rdc += img[p.img_index(size0, size01)];
                    }
                    else sum_sig += img[p.img_index(size0, size01)];
                }
                p = p.parent;
            }

            //if (seg.parent == null || sum_rdc == 0 || (sum_sig / sum_rdc >= SR_ratio && sum_sig >= byte.MaxValue))
            if (seg.parent == null || sum_rdc == 0 || (sum_sig / sum_rdc >= SR_ratio))
            {
                tolSumSig += sum_sig;
                tolSumRdc += sum_rdc;
                List<Marker> seg_markers = new();
                p = leafMarker;
                while (p != rootMarker)
                {
                    if (tmpimg[p.img_index(size0, size01)] != 0)
                    {
                        seg_markers.Add(p);
                    }
                    p = p.parent;
                }

                foreach (var marker in seg_markers)
                {
                    p = marker;
                    int r = (int)p.radius;
                    if (r > 0)
                    {
                        int x = (int)(p.position.x);
                        int y = (int)(p.position.y);
                        int z = (int)(p.position.z);
                        //double sum_sphere_size = 0;
                        //double sum_delete_size = 0;
                        for (int ii = -r; ii <= r; ii++)
                        {
                            int x2 = x + ii;
                            if (x2 < 0 || x2 >= size0) continue;
                            for (int jj = -r; jj <= r; jj++)
                            {
                                int y2 = y + jj;
                                if (y2 < 0 || y2 >= size1) continue;
                                for (int kk = -r; kk <= r; kk++)
                                {
                                    int z2 = z + kk;
                                    if (z2 < 0 || z2 >= size2) continue;
                                    if (ii * ii + jj * jj + kk * kk > r * r) continue;
                                    int index = z2 * size01 + y2 * size0 + x2;
                                    tmpimg[index] = 0;
                                }
                            }
                        }
                    }
                }

                outSegs.Add(seg);
                visitedSegs.Add(seg);
            }
        }
        ////evaluation
        //double tree_sig = 0;
        //double covered_sig = 0;
        //foreach (var m in inswc)
        //{
        //    tree_sig += img[m.img_index(size0, size01)];
        //    if (tmpimg[m.img_index(size0, size01)] == 0) covered_sig += img[m.img_index(size0, size01)];
        //}

        ////Debug.Log("S/T ratio" + covered_sig / tree_sig + "(" + covered_sig + "/" + tree_sig + ")");
        ////Debug.Log(outSegs.Count);

        var outswc = TopoSegs2swc(outSegs, 0);
        return outswc;
    }

    public List<Marker> Resample(List<Marker> inswc, byte[] img, int sz0, int sz1, int sz2,int factor =10)
    {
        int sz01 = sz0 * sz1;
        int tolSz = sz01 * sz2;
        List<HierarchySegment> topoSegs = SWC2TopoSegs(inswc, img, sz0, sz1, sz2);
        topoSegs = Resample(topoSegs, factor);
        List<Marker> outswc = TopoSegs2swc(topoSegs, 0);
        //topo_segs2swc(visited_segs,filter_segs, out outswc, 0);
        return outswc;
    }

    public List<HierarchySegment> Resample(List<HierarchySegment> inSegs, int factor)
    {
        foreach (var seg in inSegs)
        {
            if (seg.rootMarker.parent != null) seg.rootMarker.parent.isBranch = true;
            seg.rootMarker.isSegment_root = true;
        }
        foreach (var seg in inSegs)  
        {
            Marker marker = seg.leafMarker;
            Marker leafMarker = seg.leafMarker;
            Marker rootMarker = seg.rootMarker;
            marker.isLeaf = true;
            while (marker != seg.rootMarker)
            {
                Marker tailMarker = marker;
                double length = 0;
                Marker preMarker = marker;
                int countMarker = 0;
                while (marker != seg.rootMarker) //&& marker.isBranch == false
                {
                    length += Vector3.Distance(marker.position, marker.parent.position);
                    countMarker++;
                    marker = marker.parent;
                    if (marker.isBranch) break;
                }

                int count = countMarker / factor;
                double step = length / count;

                
                while (preMarker != marker)
                {
                    Marker tempMarker = preMarker;
                    double distance = 0;
                    while (distance < step && tempMarker != marker)
                    {
                        distance += Vector3.Distance(tempMarker.position, tempMarker.parent.position);
                        tempMarker = tempMarker.parent;
                    }
                    preMarker.parent = tempMarker;
                    preMarker = tempMarker;
                }

                Marker previous = null;
                Marker current = tailMarker;
                while (current.parent != marker)
                {
                    previous = current;
                    current = current.parent;
                }
                if (previous != null  && Vector3.Distance(current.position, marker.position) < step/2)
                {
                    previous.parent = marker;
                }
                
                current = tailMarker;
                while (current.parent != marker)
                {
                    previous = current;
                    current = current.parent;
                }
                //
                if (marker.isBranch == false && marker == seg.rootMarker && marker.parent != null &&
                    Vector3.Distance(marker.position, marker.parent.position) < step / 2)
                {
                    current.parent = marker.parent;
                    seg.rootMarker = current;
                    marker = current;
                }
                
            }
        }
        
        // foreach (var seg in inSegs)  
        // {
        //     Marker marker = seg.leafMarker;
        //     Marker leafMarker = seg.leafMarker;
        //     Marker rootMarker = seg.rootMarker;
        //     marker.isLeaf = true;
        //     while (marker != seg.rootMarker)
        //     {
        //         Marker tailMarker = marker;
        //         double length = 0;
        //         Marker preMarker = marker;
        //         int countMarker = 0;
        //         while (marker != seg.rootMarker)
        //         {
        //             length += Vector3.Distance(marker.position, marker.parent.position);
        //             countMarker++;
        //             marker = marker.parent;
        //             if (marker.isBranch) break;
        //         }
        //
        //         int count = countMarker / factor;
        //         double step = length / count;
        //
        //         
        //         while (preMarker != marker)
        //         {
        //             Marker tempMarker = preMarker;
        //             double distance = 0;
        //             while (distance < step && tempMarker != marker)
        //             {
        //                 distance += Vector3.Distance(tempMarker.position, tempMarker.parent.position);
        //                 tempMarker = tempMarker.parent;
        //             }
        //             preMarker.parent = tempMarker;
        //             preMarker = tempMarker;
        //         }
        //
        //         Marker previous = null;
        //         Marker current = tailMarker;
        //         while (current.parent != marker)
        //         {
        //             previous = current;
        //             current = current.parent;
        //         }
        //         if (previous != null  && Vector3.Distance(current.position, marker.position) < step/2)
        //         {
        //             previous.parent = marker;
        //         }
        //         
        //         current = tailMarker;
        //         while (current.parent != marker)
        //         {
        //             previous = current;
        //             current = current.parent;
        //         }
        //         //
        //         if (marker == seg.rootMarker && marker.parent != null &&
        //             Vector3.Distance(marker.position, marker.parent.position) < step / 2)
        //         {
        //             current.parent = marker.parent;
        //             seg.rootMarker = current;
        //             marker = current;
        //         }
        //         
        //     }
        // }

        return inSegs;
    }
}
