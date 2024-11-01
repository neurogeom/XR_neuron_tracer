using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using MathNet.Numerics.LinearAlgebra.Double;
using UnityEditor;
using MathNet.Numerics.LinearAlgebra.Solvers;

public class FastMarching
{
    int sz0, sz1, sz2, sz01;
    Texture3D SDF;
    double max_intensity;
    double min_intensity;
    int[] parent_oc;
    List<int>[] children_oc;
    HeapElemX[] elems;
    public enum States { ALIVE = -1, TRIAL = 0, FAR = 1, REPAIRED = 2 };

    ConcurrentBag<int> targetBag;

    public Dictionary<int, Marker> markers;

    public States[] state;
    public int[] parent;
    public float[] phi;
    public float[] gwdt;
    public HashSet<int> results;

    byte[] gsdt_float;

    public float[] FastMarching_dt(byte[] img, int sz0, int sz1, int sz2, int bkg_thresh = 0)
    {
        int tol_sz = sz0 * sz1 * sz2;
        int sz01 = sz0 * sz1;
        States[] state = new States[tol_sz];
        int bkg_count = 0;
        int bdr_count = 0;
        float[] gsdt = new float[tol_sz];
        for (int i = 0; i < sz0; i++)
        {
            for (int j = 0; j < sz1; j++)
            {
                for (int k = 0; k < sz2; k++)
                {
                    int index = k * sz01 + j * sz0 + i;
                    if (index > img.Length) Debug.Log(index);
                    if (img[index] < bkg_thresh)
                    {
                        gsdt[index] = img[index];
                        state[index] = States.ALIVE;
                        bkg_count++;
                    }
                    else
                    {
                        gsdt[index] = float.MaxValue;
                        //gsdt[index] = 0;
                        state[index] = States.FAR;
                    }
                }
            }
        }
        int alive = 0;
        Heap<HeapElem> heap = new Heap<HeapElem>();
        var elems = new Dictionary<int, HeapElem>();
        for (int i = 0; i < sz0; i++)
        {
            for (int j = 0; j < sz1; j++)
            {
                for (int k = 0; k < sz2; k++)
                {
                    int index = k * sz01 + j * sz0 + i;
                    if (state[index] == States.ALIVE)
                    {
                        alive++;
                        for (int ii = -1; ii <= 1; ii++)
                        {
                            int i2 = i + ii;
                            if (i2 < 0 || i2 >= sz0) continue;
                            for (int jj = -1; jj <= 1; jj++)
                            {
                                int j2 = j + jj;
                                if (j2 < 0 || j2 >= sz1) continue;
                                for (int kk = -1; kk <= 1; kk++)
                                {
                                    int k2 = k + kk;
                                    if (k2 < 0 || k2 >= sz2) continue;
                                    int offset = Math.Abs(ii) + Math.Abs(jj) + Math.Abs(kk);
                                    if (offset > 2) continue;   //connection type=2?
                                    int index_2 = (int)(k2 * sz01 + j2 * sz0 + i2);
                                    if (state[index_2] == States.FAR)
                                    {
                                        int mini = i, minj = j, mink = k;
                                        int index_min = mink * sz01 + minj * sz0 + mini;
                                        if (gsdt[index_min] > 0)
                                        {
                                            for (int iii = -1; iii <= 1; iii++)
                                            {
                                                int i3 = i2 + iii;
                                                if (i3 < 0 || i3 >= sz0) continue;
                                                for (int jjj = -1; jjj <= 1; jjj++)
                                                {
                                                    int j3 = j2 + jjj;
                                                    if (j3 < 0 || j3 >= sz1) continue;
                                                    for (int kkk = -1; kkk <= 1; kkk++)
                                                    {
                                                        int k3 = k2 + kkk;
                                                        if (k3 < 0 || k3 >= sz2) continue;
                                                        int offset2 = Mathf.Abs(iii) + Mathf.Abs(jjj) + Mathf.Abs(kkk);
                                                        if (offset2 > 2) continue;   //connection type=2?
                                                        int index_3 = k3 * sz01 + j3 * sz0 + i3;
                                                        if (state[index_3] == States.ALIVE && gsdt[index_3] < gsdt[index_min])
                                                        {
                                                            index_min = index_3;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        gsdt[index_2] = gsdt[index_min] + img[index_2];
                                        state[index_2] = States.TRIAL;
                                        HeapElem elem = new HeapElem(index_2, gsdt[index_2]);
                                        heap.insert(elem);

                                        elems.Add(index_2, elem);
                                        bdr_count++;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        Debug.Log("bkg_count: " + bkg_count);
        Debug.Log("bdr_count: " + bdr_count);

        int time_counter = bkg_count;
        while (!heap.empty())
        {
            double process2 = (time_counter++) * 100000.0 / tol_sz;
            //heap.RemoveAt(heap.keys[0]);
            HeapElem min_elem = heap.delete_min();
            elems.Remove(min_elem.img_index);

            int min_index = min_elem.img_index;
            state[min_index] = States.ALIVE;
            int i = (int)(min_index % sz0);
            int j = (int)((min_index / sz0) % sz1);
            int k = (int)((min_index / sz01) % sz2);
            for (int ii = -1; ii <= 1; ii++)
            {
                int w = i + ii;
                if (w < 0 || w >= sz0) continue;
                for (int jj = -1; jj <= 1; jj++)
                {
                    int h = j + jj;
                    if (h < 0 || h >= sz1) continue;
                    for (int kk = -1; kk <= 1; kk++)
                    {
                        int d = k + kk;
                        if (d < 0 || d >= sz2) continue;
                        int offset = Mathf.Abs(ii) + Mathf.Abs(jj) + Mathf.Abs(kk);
                        if (offset > 2) continue;   //connection type=2?
                        int index = d * sz01 + h * sz0 + w;

                        if (state[index] != States.ALIVE)
                        {
                            float new_dist = gsdt[min_index] + img[index] * Mathf.Sqrt(offset);

                            if (state[index] == States.FAR)
                            {
                                gsdt[index] = new_dist;
                                HeapElem elem = new HeapElem((int)index, new_dist);
                                heap.insert(elem);
                                elems.Add((int)index, elem);
                                state[index] = States.TRIAL;
                            }
                            else if (state[index] == States.TRIAL)
                            {
                                if (gsdt[index] > new_dist)
                                {
                                    gsdt[index] = new_dist;
                                    HeapElem elem;
                                    elems.TryGetValue((int)index, out elem);
                                    heap.adjust(elem.heap_id, gsdt[index]);
                                }
                            }
                        }
                    }
                }
            }
        }
        return gsdt;
    }

    public float[] FastMarching_dt_parallel(byte[] img, int sz0, int sz1, int sz2, int bkg_thresh = 0)
    {
        int tol_sz = sz0 * sz1 * sz2;
        int sz01 = sz0 * sz1;
        States[] state = new States[tol_sz];
        int bkg_count = 0;

        Debug.Log(tol_sz);
        float[] gsdt = new float[tol_sz];
        Parallel.For(0, tol_sz, index =>
        {
            if (index > img.Length) Debug.Log(index);
            if (img[index] < bkg_thresh)
            {
                gsdt[index] = img[index];
                state[index] = States.ALIVE;
            }
            else
            {
                gsdt[index] = float.MaxValue;
                //gsdt[index] = 0;
                state[index] = States.FAR;
            }
        });

        Heap<HeapElem> heap = new Heap<HeapElem>();
        var elems = new ConcurrentDictionary<int, HeapElem>();
        var concurrentBag = new ConcurrentBag<HeapElem>();

        Parallel.For(0, sz0, i =>
        {
            for (int j = 0; j < sz1; j++)
            {
                for (int k = 0; k < sz2; k++)
                {
                    int index = k * sz01 + j * sz0 + i;
                    if (state[index] == States.ALIVE)
                    {
                        for (int ii = -1; ii <= 1; ii++)
                        {
                            int i2 = (int)i + ii;
                            if (i2 < 0 || i2 >= sz0) continue;
                            for (int jj = -1; jj <= 1; jj++)
                            {
                                int j2 = j + jj;
                                if (j2 < 0 || j2 >= sz1) continue;
                                for (int kk = -1; kk <= 1; kk++)
                                {
                                    int k2 = k + kk;
                                    if (k2 < 0 || k2 >= sz2) continue;
                                    int offset = Math.Abs(ii) + Math.Abs(jj) + Math.Abs(kk);
                                    if (offset > 2) continue;   //connection type=2?
                                    int index_2 = k2 * sz01 + j2 * sz0 + i2;
                                    if (state[index_2] == States.FAR)
                                    {
                                        int mini = (int)i, minj = j, mink = k;
                                        int index_min = mink * sz01 + minj * sz0 + mini;
                                        if (gsdt[index_min] > 0)
                                        {
                                            for (int iii = -1; iii <= 1; iii++)
                                            {
                                                int i3 = i2 + iii;
                                                if (i3 < 0 || i3 >= sz0) continue;
                                                for (int jjj = -1; jjj <= 1; jjj++)
                                                {
                                                    int j3 = j2 + jjj;
                                                    if (j3 < 0 || j3 >= sz1) continue;
                                                    for (int kkk = -1; kkk <= 1; kkk++)
                                                    {
                                                        int k3 = k2 + kkk;
                                                        if (k3 < 0 || k3 >= sz2) continue;
                                                        int offset2 = Mathf.Abs(iii) + Mathf.Abs(jjj) + Mathf.Abs(kkk);
                                                        if (offset2 > 2) continue;   //connection type=2?
                                                        int index_3 = k3 * sz01 + j3 * sz0 + i3;
                                                        if (state[index_3] == States.ALIVE && gsdt[index_3] < gsdt[index_min])
                                                        {
                                                            index_min = index_3;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        gsdt[index_2] = gsdt[index_min] + img[index_2];
                                        state[index_2] = States.TRIAL;
                                        HeapElem elem = new HeapElem((int)index_2, gsdt[index_2]);

                                        //heap.insert(elem);

                                        if (elems.TryAdd((int)index_2, elem)) concurrentBag.Add(elem);
                                        //elems.Add(index_2, elem);

                                        //bdr_count++;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        });


        //Debug.Log("bkg_count: " + bkg_count);
        //Debug.Log("bdr_count: " + bdr_count);
        Debug.Log(concurrentBag.Count);
        foreach (var elem in concurrentBag)
        {
            heap.insert(elem);
        }

        int time_counter = bkg_count;
        while (!heap.empty())
        {
            double process2 = (time_counter++) * 100000.0 / tol_sz;
            //heap.RemoveAt(heap.keys[0]);
            HeapElem min_elem = heap.delete_min();
            HeapElem temp;
            elems.TryRemove(min_elem.img_index, out temp);
            //elems.Remove(min_elem.img_index);

            int min_index = min_elem.img_index;
            state[min_index] = States.ALIVE;
            int i = (int)(min_index % sz0);
            int j = (int)((min_index / sz0) % sz1);
            int k = (int)((min_index / sz01) % sz2);
            for (int ii = -1; ii <= 1; ii++)
            {
                int w = i + ii;
                if (w < 0 || w >= sz0) continue;
                for (int jj = -1; jj <= 1; jj++)
                {
                    int h = j + jj;
                    if (h < 0 || h >= sz1) continue;
                    for (int kk = -1; kk <= 1; kk++)
                    {
                        int d = k + kk;
                        if (d < 0 || d >= sz2) continue;
                        int offset = Mathf.Abs(ii) + Mathf.Abs(jj) + Mathf.Abs(kk);
                        if (offset > 2) continue;   //connection type=2?
                        int index = d * sz01 + h * sz0 + w;

                        if (state[index] != States.ALIVE)
                        {
                            float new_dist = gsdt[min_index] + img[index] * Mathf.Sqrt(offset);

                            if (state[index] == States.FAR)
                            {
                                gsdt[index] = new_dist;
                                HeapElem elem = new HeapElem(index, new_dist);
                                heap.insert(elem);
                                elems.TryAdd((int)index, elem);
                                state[index] = States.TRIAL;
                            }
                            else if (state[index] == States.TRIAL)
                            {
                                if (gsdt[index] > new_dist)
                                {
                                    gsdt[index] = new_dist;
                                    HeapElem elem;
                                    elems.TryGetValue((int)index, out elem);
                                    heap.adjust(elem.heap_id, gsdt[index]);
                                }
                            }
                        }
                    }
                }
            }
        }
        Debug.Log("gwdt done");

        return gsdt;
    }

    public double[] MSFM_dt_parallel(byte[] img, int sz0, int sz1, int sz2, int bkg_thresh = 0)
    {
        int tol_sz = sz0 * sz1 * sz2;
        int sz01 = sz0 * sz1;
        States[] state = new States[tol_sz];
        int bkg_count = 0;
        int bdr_count = 0;
        double[] gsdt = new double[tol_sz];
        targetBag = new ConcurrentBag<int>();

        Debug.Log(img.Length);
        Debug.Log(tol_sz);

        Parallel.For(0, tol_sz, index =>
        {
            if (index > img.Length) Debug.Log(index);
            if (img[index] < bkg_thresh)
            {
                gsdt[index] = img[index];
                state[index] = States.ALIVE;
                bkg_count++;
            }
            else
            {
                gsdt[index] = double.MaxValue;
                //gsdt[index] = 0;
                state[index] = States.FAR;
                targetBag.Add((int)index);
            }
        });

        Heap<HeapElem> heap = new Heap<HeapElem>();
        var elems = new ConcurrentDictionary<int, HeapElem>();
        var concurrentBag = new ConcurrentBag<HeapElem>();

        Parallel.For(0, sz0, i =>
        {
            for (int j = 0; j < sz1; j++)
            {
                for (int k = 0; k < sz2; k++)
                {
                    int index = k * sz01 + j * sz0 + i;
                    if (state[index] == States.ALIVE)
                    {
                        for (int ii = -1; ii <= 1; ii++)
                        {
                            int i2 = (int)i + ii;
                            if (i2 < 0 || i2 >= sz0) continue;
                            for (int jj = -1; jj <= 1; jj++)
                            {
                                int j2 = j + jj;
                                if (j2 < 0 || j2 >= sz1) continue;
                                for (int kk = -1; kk <= 1; kk++)
                                {
                                    int k2 = k + kk;
                                    if (k2 < 0 || k2 >= sz2) continue;
                                    int offset = Math.Abs(ii) + Math.Abs(jj) + Math.Abs(kk);
                                    if (offset > 2) continue;   //connection type=2?
                                    int index_2 = k2 * sz01 + j2 * sz0 + i2;
                                    if (state[index_2] == States.FAR)
                                    {
                                        int mini = (int)i, minj = j, mink = k;
                                        int index_min = mink * sz01 + minj * sz0 + mini;
                                        if (gsdt[index_min] > 0)
                                        {
                                            for (int iii = -1; iii <= 1; iii++)
                                            {
                                                int i3 = i2 + iii;
                                                if (i3 < 0 || i3 >= sz0) continue;
                                                for (int jjj = -1; jjj <= 1; jjj++)
                                                {
                                                    int j3 = j2 + jjj;
                                                    if (j3 < 0 || j3 >= sz1) continue;
                                                    for (int kkk = -1; kkk <= 1; kkk++)
                                                    {
                                                        int k3 = k2 + kkk;
                                                        if (k3 < 0 || k3 >= sz2) continue;
                                                        int offset2 = Mathf.Abs(iii) + Mathf.Abs(jjj) + Mathf.Abs(kkk);
                                                        if (offset2 > 2) continue;   //connection type=2?
                                                        int index_3 = k3 * sz01 + j3 * sz0 + i3;
                                                        if (state[index_3] == States.ALIVE && gsdt[index_3] < gsdt[index_min])
                                                        {
                                                            index_min = index_3;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        gsdt[index_2] = gsdt[index_min] + img[index_2];
                                        state[index_2] = States.TRIAL;
                                        HeapElem elem = new HeapElem((int)index_2, gsdt[index_2]);

                                        //heap.insert(elem);

                                        if (elems.TryAdd((int)index_2, elem))
                                        {
                                            concurrentBag.Add(elem);
                                            bdr_count++;
                                        }
                                        //elems.Add(index_2, elem);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        });


        Debug.Log("bkg_count: " + bkg_count);
        Debug.Log("bdr_count: " + bdr_count);
        Debug.Log(concurrentBag.Count);
        foreach (var elem in concurrentBag)
        {
            heap.insert(elem);
        }

        int time_counter = bkg_count;

        while (!heap.empty())
        {
            double process2 = (time_counter++) * 100000.0 / tol_sz;
            //heap.RemoveAt(heap.keys[0]);
            HeapElem min_elem = heap.delete_min();
            HeapElem temp;
            elems.TryRemove(min_elem.img_index, out temp);
            //elems.Remove(min_elem.img_index);

            int min_index = min_elem.img_index;
            state[min_index] = States.ALIVE;
            int i = (int)(min_index % sz0);
            int j = (int)((min_index / sz0) % sz1);
            int k = (int)((min_index / sz01) % sz2);
            for (int ii = -1; ii <= 1; ii++)
            {
                int w = i + ii;
                if (w < 0 || w >= sz0) continue;
                for (int jj = -1; jj <= 1; jj++)
                {
                    int h = j + jj;
                    if (h < 0 || h >= sz1) continue;
                    for (int kk = -1; kk <= 1; kk++)
                    {
                        int d = k + kk;
                        if (d < 0 || d >= sz2) continue;
                        int offset = Mathf.Abs(ii) + Mathf.Abs(jj) + Mathf.Abs(kk);
                        if (offset > 2) continue;   //connection type=2?
                        int index = d * sz01 + h * sz0 + w;

                        if (state[index] != States.ALIVE)
                        {
                            double new_dist = gsdt[min_index] + img[index] * Mathf.Sqrt(offset);

                            if (state[index] == States.FAR)
                            {
                                gsdt[index] = new_dist;
                                HeapElem elem = new HeapElem((int)index, new_dist);
                                heap.insert(elem);
                                elems.TryAdd((int)index, elem);
                                state[index] = States.TRIAL;
                            }
                            else if (state[index] == States.TRIAL)
                            {
                                if (gsdt[index] > new_dist)
                                {
                                    gsdt[index] = new_dist;
                                    HeapElem elem;
                                    elems.TryGetValue((int)index, out elem);
                                    heap.adjust(elem.heap_id, gsdt[index]);
                                }
                            }
                        }
                    }
                }
            }
        }
        Debug.Log("occupancy gwdt done");
        return gsdt;
    }

    public List<Marker> FastMarching_tree(Marker root, float[] img, int _sz0, int _sz1, int _sz2, HashSet<int> targets,
                                        int cnn_type = 3, int bkg_thresh = 30, bool is_break_accept = false)
    {
        //double higher_thresh = 150;
        sz0 = _sz0;
        sz1 = _sz1;
        sz2 = _sz2;
        int tol_sz = sz0 * sz1 * sz2;
        sz01 = sz0 * sz1;

        gwdt = new float[tol_sz];
        phi = new float[tol_sz];
        parent = new int[tol_sz];
        state = new States[tol_sz];

        img.CopyTo(gwdt, 0);

        var outTree = new List<Marker>();

        max_intensity = 0;
        min_intensity = double.MaxValue;

        Parallel.For(0, tol_sz, i =>
        {
            phi[i] = float.MaxValue;
            parent[i] = (int)i;  // each pixel point to itself at the         statements beginning
            state[i] = States.FAR;
            max_intensity = Math.Max(max_intensity, gwdt[i]);
            min_intensity = Math.Min(min_intensity, gwdt[i]);
        });

        //for (int i = 0; i < tol_sz; i++)
        //{
        //    phi[i] = float.MaxValue;
        //    parent[i] = i;  // each pixel point to itself at the         statements beginning
        //    state[i] = States.FAR;
        //    max_intensity = Math.Max(max_intensity, gsdt[i]);
        //    min_intensity = Math.Min(min_intensity, gsdt[i]);
        //}

        max_intensity -= min_intensity;
        
        //root.pos += new Vector3(0.5f,0.5f,0.5f);
        int root_index = (int)((int)root.position.z * sz01 + (int)root.position.y * sz0 + (int)root.position.x);
        state[root_index] = States.ALIVE;
        phi[root_index] = 0;

        SortedSet<int> target_set = new SortedSet<int>(new TargetComparer(sz0, sz1, sz2, root));

        foreach(var i in targets){
            if (i != root_index) target_set.Add(i);
        }

        Heap<HeapElemX> heap = new Heap<HeapElemX>();
        var elems = new Dictionary<int, HeapElemX>();
        //init heap
        HeapElemX rootElem = new HeapElemX((int)root_index, phi[root_index]);
        rootElem.prev_index = root_index;
        heap.insert(rootElem);
        elems[(int)root_index] = rootElem;
        results = new HashSet<int>();
        Debug.Log(target_set.Count);
        while (!heap.empty())
        {
            HeapElemX min_elem = heap.delete_min();
            elems.Remove(min_elem.img_index);
            results.Add(min_elem.img_index);

            //insert target
            if (target_set.Contains(min_elem.img_index)) target_set.Remove(min_elem.img_index);

            int min_index = min_elem.img_index;

            parent[min_index] = min_elem.prev_index;

            state[min_index] = States.ALIVE;

            int i = (int)(min_index % sz0);
            int j = (int)((min_index / sz0) % sz1);
            int k = (int)((min_index / sz01) % sz2);

            int w, h, d;
            for (int ii = -1; ii <= 1; ii++)
            {
                w = i + ii;
                if (w < 0 || w >= sz0) continue;
                for (int jj = -1; jj <= 1; jj++)
                {
                    h = j + jj;
                    if (h < 0 || h >= sz1) continue;
                    for (int kk = -1; kk <= 1; kk++)
                    {
                        d = k + kk;
                        if (d < 0 || d >= sz2) continue;
                        int offset = Math.Abs(ii) + Math.Abs(jj) + Math.Abs(kk);
                        if (offset == 0 || offset > cnn_type) continue;
                        double factor = (offset == 1) ? 1.0 : ((offset == 2) ? 1.414214 : ((offset == 3) ? 1.732051 : 0.0));
                        int index = (int)(d * sz01 + h * sz0 + w);
                        int marker_distance = (int)Vector3.Distance(root.position, new Vector3(w, h, d));
                        //double true_thresh;
                        //true_thresh = marker_distance <= 50 ? higher_thresh : bkg_thresh;
                        if (is_break_accept)
                        {

                            if (gwdt[index] < bkg_thresh && gwdt[min_index] < bkg_thresh) continue;
                        }
                        else
                        {
                            if (gwdt[index] < bkg_thresh) continue;
                        }
                        if (state[index] != States.ALIVE)
                        {
                            float new_dist = (float)(phi[min_index] + (GI(gwdt[index]) + GI(gwdt[min_index])) * factor * 0.5);
                            int prev_index = min_index;

                            if (state[index] == States.FAR)
                            {
                                phi[index] = new_dist;
                                HeapElemX elem = new HeapElemX(index, phi[index]);
                                elem.prev_index = prev_index;
                                heap.insert(elem);
                                elems[index] = elem;
                                state[index] = States.TRIAL;
                            }
                            else if (state[index] == States.TRIAL)
                            {
                                if (phi[index] > new_dist)
                                {
                                    phi[index] = new_dist;
                                    HeapElemX elem = elems[index];
                                    heap.adjust(elem.heap_id, phi[index]);
                                    elem.prev_index = prev_index;
                                }
                            }
                        }
                    }
                }
            }
        }
        Debug.Log("fast Marching done"+ "result count:"+ results.Count);

        {
            //gsdt_float = new byte[gsdt.Length];
            //Texture3D texture3D = new Texture3D(sz0, sz1, sz2, TextureFormat.R8, false);
            //texture3D.wrapMode = TextureWrapMode.Clamp;
            //for (int i = 0; i < gsdt.Length; i++)
            //{
            //    //gsdt[i] = (float)(gsdt[i] / maximum);
            //    if (state[i] == States.ALIVE)
            //    {
            //        gsdt_float[i] = 255;
            //    }
            //    else gsdt_float[i] = 0;
            //}
            //texture3D.SetPixelData(gsdt_float, 0);
            //texture3D.Apply();
            //AssetDatabase.DeleteAsset("Assets/Textures/" + "initial" + ".Asset");
            //AssetDatabase.CreateAsset(texture3D, "Assets/Textures/" + "initial" + ".Asset");
            //AssetDatabase.SaveAssets();
            //AssetDatabase.Refresh();
        }

        var searchSet = new HashSet<int>();
        var connection = new Dictionary<int, int>();
        Debug.Log(target_set.Count);
        //while (target_set.Count > 0)
        //{
        //    float min_dis = float.MaxValue;

        //    int target_index = target_set.Last();
        //    //createSphere(IndexToVector(target_index), new Vector3Int(sz0, sz1, sz2), Color.cyan, 0.05f);

        //    //int target_index = target_set.First();
        //    target_set.Remove(target_index);
        //    HashSet<Vector3> voxelSet = findSubVoxels(target_index, gwdt, searchSet, target_set, results, sz0, sz1, sz2, bkg_thresh);
        //    Debug.Log("voxel count:"+voxelSet.Count);
        //    if (voxelSet.Count < 3) continue;

        //    //trace back to trunk
        //    SearchCluster(voxelSet, -1, root, new Vector3Int(sz0, sz1, sz2), new Vector3Int(o_width, o_height, o_depth), searchSet, results, target_set, heap, elems, connection, bkg_thresh);

        //    Debug.Log(heap.elems.Count);
        //    (Vector3 direction, Vector3 maximum_pos, Vector3 minimum_pos) = PCA(voxelSet, new Vector3Int(sz0, sz1, sz2), new Vector3Int(o_width, o_height, o_depth));
             
        //    int serachLength = 25;
        //    SearchCluster(minimum_pos, -direction, serachLength, gwdt, searchSet, results, target_set, bkg_thresh);
        //    SearchCluster(maximum_pos, direction, serachLength, gwdt, searchSet, results, target_set, bkg_thresh);
        //    Debug.Log("target_set count befor heap:" + target_set.Count);

        //    while (!heap.empty())
        //    {
        //        HeapElemX min_elem = heap.delete_min();
        //        elems.Remove(min_elem.img_index);
        //        results.Add(min_elem.img_index);

        //        //insert target
        //        if (target_set.Contains(min_elem.img_index)) target_set.Remove(min_elem.img_index);

        //        int min_index = min_elem.img_index;

        //        parent[min_index] = min_elem.prev_index;

        //        state[min_index] = States.ALIVE;

        //        int i = (int)(min_index % sz0);
        //        int j = (int)((min_index / sz0) % sz1);
        //        int k = (int)((min_index / sz01) % sz2);

        //        int w, h, d;
        //        for (int ii = -1; ii <= 1; ii++)
        //        {
        //            w = i + ii;
        //            if (w < 0 || w >= sz0) continue;
        //            for (int jj = -1; jj <= 1; jj++)
        //            {
        //                h = j + jj;
        //                if (h < 0 || h >= sz1) continue;
        //                for (int kk = -1; kk <= 1; kk++)
        //                {
        //                    d = k + kk;
        //                    if (d < 0 || d >= sz2) continue;
        //                    int offset = Math.Abs(ii) + Math.Abs(jj) + Math.Abs(kk);
        //                    if (offset == 0 || offset > cnn_type) continue;
        //                    double factor = (offset == 1) ? 1.0 : ((offset == 2) ? 1.414214 : ((offset == 3) ? 1.732051 : 0.0));
        //                    int index = (int)(d * sz01 + h * sz0 + w);
        //                    int marker_distance = (int)Vector3.Distance(root.position, new Vector3(w, h, d));
        //                    //double true_thresh;
        //                    //true_thresh = marker_distance <= 50 ? higher_thresh : bkg_thresh;
        //                    if (is_break_accept)
        //                    {

        //                        if (gwdt[index] < bkg_thresh && gwdt[min_index] < bkg_thresh) continue;
        //                    }
        //                    else
        //                    {
        //                        if (gwdt[index] < bkg_thresh) continue;
        //                    }
        //                    if (state[index] != States.ALIVE)
        //                    {
        //                        float new_dist = (float)(phi[min_index] + (GI(gwdt[index]) + GI(gwdt[min_index])) * factor * 0.5);
        //                        int prev_index = min_index;

        //                        if (state[index] == States.FAR)
        //                        {
        //                            phi[index] = new_dist;
        //                            HeapElemX elem = new HeapElemX(index, phi[index]);
        //                            elem.prev_index = prev_index;
        //                            heap.insert(elem);
        //                            elems[index] = elem;
        //                            state[index] = States.TRIAL;
        //                        }
        //                        else if (state[index] == States.TRIAL)
        //                        {
        //                            if (phi[index] > new_dist)
        //                            {
        //                                phi[index] = new_dist;
        //                                HeapElemX elem = elems[index];
        //                                heap.adjust(elem.heap_id, phi[index]);
        //                                elem.prev_index = prev_index;
        //                            }
        //                        }
        //                    }
        //                }
        //            }
        //        }

        //        if (connection.ContainsKey(min_index))
        //        {
        //            //Debug.Log("connection works");
        //            int index = connection[min_index];
        //            w = (int)(min_index % sz0);
        //            h = (int)((min_index / sz0) % sz1);
        //            d = (int)((min_index / sz01) % sz2);
        //            double factor = Vector3.Distance(new Vector3(i, j, k), new Vector3(w, h, d));
        //            if (state[index] != States.ALIVE)
        //            {
        //                float new_dist = (float)(phi[min_index] + (GI(gwdt[index]) + GI(gwdt[min_index])) * factor * 0.5);
        //                int prev_index = min_index;

        //                if (state[index] == States.FAR)
        //                {
        //                    phi[index] = new_dist;
        //                    HeapElemX elem = new HeapElemX(index, phi[index]);
        //                    elem.prev_index = prev_index;
        //                    heap.insert(elem);
        //                    elems[index] = elem;
        //                    state[index] = States.TRIAL;
        //                }
        //                else if (state[index] == States.TRIAL)
        //                {
        //                    if (phi[index] > new_dist)
        //                    {
        //                        phi[index] = new_dist;
        //                        HeapElemX elem = elems[index];
        //                        heap.adjust(elem.heap_id, phi[index]);
        //                        elem.prev_index = prev_index;
        //                    }
        //                }
        //            }
        //        }
        //    }

        //    Debug.Log("target_set count befor heap:" + target_set.Count);
        //}

        //texture3D = new Texture3D(sz0, sz1, sz2, TextureFormat.R8, false);
        //texture3D.wrapMode = TextureWrapMode.Clamp;
        //for (int i = 0; i < gsdt.Length; i++)
        //{
        //    //gsdt[i] = (float)(gsdt[i] / maximum);
        //    if (gsdt_float[i] == 255) gsdt_float[i] = 128;
        //    else if (state[i] == States.ALIVE && gsdt_float[i] == 0)
        //    {
        //        gsdt_float[i] = 255;
        //    }
        //    else gsdt_float[i] = 0;
        //}
        //texture3D.SetPixelData(gsdt_float, 0);
        //texture3D.Apply();
        //AssetDatabase.DeleteAsset("Assets/Textures/" + "after" + ".Asset");
        //AssetDatabase.CreateAsset(texture3D, "Assets/Textures/" + "after" + ".Asset");
        //AssetDatabase.SaveAssets();
        //AssetDatabase.Refresh();

        //for (int i = 0; i < gsdt.Length; i++)
        //{
        //    //gsdt[i] = (float)(gsdt[i] / maximum);
        //    if (gsdt_float[i] == 255) gsdt_float[i] = 128;
        //    else if (state[i] == States.ALIVE && gsdt_float[i] == 0)
        //    {
        //        gsdt_float[i] = 255;
        //    }
        //    else gsdt_float[i] = 0;
        //}

        //save swc tree
        markers = new Dictionary<int, Marker>();

        foreach (var index in results)
        {
            if (state[index] != States.ALIVE) continue;
            int i = (int)(index % sz0);
            int j = (int)((index / sz0) % sz1);
            int k = (int)((index / sz01) % sz2);
            Marker marker = new Marker(new Vector3(i, j, k));
            markers[index] = marker;
            outTree.Add(marker);
        }

        foreach (var index in results)
        {
            if (state[index] != States.ALIVE) continue;
            int index2 = parent[index];
            Marker marker1 = markers[index];
            Marker marker2 = markers[index2];
            if (marker1 == marker2) marker1.parent = null;
            else marker1.parent = marker2;
        }

        return outTree;
    }

    public bool MSFM_tree(Marker root, double[] img, int sz0, int sz1, int sz2,
                                int cnn_type = 3, int bkg_thresh = 30, bool is_break_accept = false)
    {
        
        int tol_sz = sz0 * sz1 * sz2;
        int sz01 = sz0 * sz1;

        double[] gwdt_oc = new double[tol_sz];
        double[] phi_oc = new double[tol_sz];
        parent_oc = new int[tol_sz];
        children_oc = new List<int>[tol_sz];
        States[] state = new States[tol_sz];

        img.CopyTo(gwdt_oc, 0);

        max_intensity = 0;
        min_intensity = double.MaxValue;

        for (int i = 0; i < tol_sz; i++)
        {
            phi_oc[i] = double.MaxValue;
            parent_oc[i] = (int)i;  // each pixel point to itself at the         statements beginning
            state[i] = States.FAR;
            max_intensity = Math.Max(max_intensity, gwdt_oc[i]);
            min_intensity = Math.Min(min_intensity, gwdt_oc[i]);
        }

        //max_intensity -= min_intensity;
        
        //root.pos += new Vector3(0.5f,0.5f,0.5f);
        int root_index = (int)((int)root.position.z * sz01 + (int)root.position.y * sz0 + (int)root.position.x);
        state[root_index] = States.ALIVE;
        phi_oc[root_index] = 0;

        Heap<HeapElemX> heap = new Heap<HeapElemX>();
        heap.elems.Capacity = (int)tol_sz;
        //Dictionary<int, HeapElemX> elems = new Dictionary<int, HeapElemX>();
        elems = new HeapElemX[tol_sz];
        //init heap
        HeapElemX rootElem = new HeapElemX(root_index, phi_oc[root_index]);
        rootElem.prev_index = root_index;
        heap.insert(rootElem);
        elems[root_index] = rootElem;
        var results = new HashSet<int>();
        Debug.Log(targetBag.Count);
        var targetSet = new HashSet<int>(targetBag.ToHashSet<int>());
        Debug.Log(targetSet.Count);
        targetBag.Clear();
        double max_dist = 0;
        while (!heap.empty() && targetSet.Count != 0)
        {
            HeapElemX min_elem = heap.delete_min();
            //elems.Remove(min_elem.img_index);
            results.Add(min_elem.img_index);
            if (targetSet.Contains(min_elem.img_index)) targetSet.Remove(min_elem.img_index);
            int min_index = min_elem.img_index;

            parent_oc[min_index] = min_elem.prev_index;
            if (children_oc[min_elem.prev_index] == null) children_oc[min_elem.prev_index] = new List<int>();
            children_oc[min_elem.prev_index].Add(min_index);

            state[min_index] = States.ALIVE;

            int i = (int)(min_index % sz0);
            int j = (int)((min_index / sz0) % sz1);
            int k = (int)((min_index / sz01) % sz2);


            int w, h, d;
            for (int ii = -1; ii <= 1; ii++)
            {
                w = i + ii;
                if (w < 0 || w >= sz0) continue;
                for (int jj = -1; jj <= 1; jj++)
                {
                    h = j + jj;
                    if (h < 0 || h >= sz1) continue;
                    for (int kk = -1; kk <= 1; kk++)
                    {
                        d = k + kk;
                        if (d < 0 || d >= sz2) continue;
                        int offset = Math.Abs(ii) + Math.Abs(jj) + Math.Abs(kk);
                        if (offset == 0 || offset > cnn_type) continue;
                        double factor = (offset == 1) ? 1.0 : ((offset == 2) ? 1.414214 : ((offset == 3) ? 1.732051 : 0.0));
                        int index = (int)(d * sz01 + h * sz0 + w);
                        int marker_distance = (int)Vector3.Distance(root.position, new Vector3(w, h, d));
                        //double true_thresh;
                        //true_thresh = marker_distance <= 50 ? higher_thresh : bkg_thresh;

                        //if (is_break_accept)
                        //{

                        //    if (gsdt[index] < bkg_thresh && gsdt[min_index] < bkg_thresh) continue;
                        //}
                        //else
                        //{
                        //    if (gsdt[index] < bkg_thresh) continue;
                        //}

                        if (state[index] != States.ALIVE)
                        {
                            double new_dist;
                            double intensity = gwdt_oc[index];
                            if (gwdt_oc[index] < bkg_thresh)
                            {
                                //new_dist = phi[min_index] + 1/0.0000000001;
                                new_dist = phi_oc[min_index] + 1 / 0.0000000001;

                            }
                            else
                            {
                                new_dist = phi_oc[min_index] + 1 / ((intensity / max_intensity) * (intensity / max_intensity) * (intensity / max_intensity));
                                //new_dist = phi[min_index] + 1 / ((intensity / max_intensity) * (intensity / max_intensity) * (intensity / max_intensity) * (intensity / max_intensity));
                                //new_dist = phi[min_index] + (GI(gsdt[index]) + GI(gsdt[min_index])) * factor * 0.5;

                            }
                            //double new_dist = phi[min_index] + (GI(gsdt[index]) + GI(gsdt[min_index])) * factor * 0.5;
                            int prev_index = min_index;
                            max_dist = Math.Max(max_dist, new_dist);
                            if (state[index] == States.FAR)
                            {
                                phi_oc[index] = new_dist;
                                HeapElemX elem = new HeapElemX(index, phi_oc[index]);
                                elem.prev_index = prev_index;
                                heap.insert(elem);
                                elems[index] = elem;
                                state[index] = States.TRIAL;
                            }
                            else if (state[index] == States.TRIAL)
                            {
                                if (phi_oc[index] > new_dist)
                                {
                                    phi_oc[index] = new_dist;
                                    HeapElemX elem = elems[index];
                                    heap.adjust(elem.heap_id, phi_oc[index]);
                                    elem.prev_index = prev_index;
                                }
                            }
                        }
                    }
                }
            }
        }
        Debug.Log("Multi-Stencils Fast Marching done");

        //float[] gsdt_float = new float[gsdt.Length];
        //Texture3D texture3D = new Texture3D(sz0, sz1, sz2, TextureFormat.RFloat, false);
        //Debug.Log(max_dist);
        //for (int i = 0; i < gsdt.Length; i++)
        //{
        //    //gsdt[i] = (float)(gsdt[i] / maximum);
        //    gsdt_float[i] = (float)(phi[i] / max_dist);
        //}
        //texture3D.SetPixelData(gsdt_float, 0);
        //texture3D.Apply();
        //AssetDatabase.DeleteAsset("Assets/Textures/" + "initial reconstrcution" + ".Asset");
        //AssetDatabase.CreateAsset(texture3D, "Assets/Textures/" + "initial reconstrcution" + ".Asset");
        //AssetDatabase.SaveAssets();
        //AssetDatabase.Refresh();

        return true;
    }

    public class TargetComparer : IComparer<int>
    {
        int sz0, sz1, sz2, sz01;
        Marker root;
        public TargetComparer(int _sz0, int _sz1, int _sz2, Marker _root)
        {
            sz0 = _sz0;
            sz1 = _sz1;
            sz2 = _sz2;
            root = _root;
            sz01 = sz0 * sz1;
        }
        public int Compare(int index1, int index2)
        {
            int x = (int)(index1 % sz0);
            int y = (int)((index1 / sz0) % sz1);
            int z = (int)((index1 / sz01) % sz2);
            float distance_toseed_1 = Vector3.Distance(new Vector3(x, y, z), root.position);

            x = (int)(index2 % sz0);
            y = (int)((index2 / sz0) % sz1);
            z = (int)((index2 / sz01) % sz2);
            float distance_toseed_2 = Vector3.Distance(new Vector3(x, y, z), root.position);

            if (distance_toseed_1 < distance_toseed_2) return -1;
            else if (distance_toseed_1 == distance_toseed_2) return 0;
            else return 1;
        }
    }

    public List<Marker> TraceTarget(List<Marker> filteredTree, out Marker branchRoot, Marker root, int targetIndex, int _sz0, int _sz1, int _sz2, int o_width, int o_height, int o_depth, int cnn_type = 3, int bkg_thresh = 30, bool is_break_accept = false)
    {
        if (results.Contains(targetIndex))
        {
            Debug.Log("Target has been traced");
            branchRoot = null;
            return new List<Marker>();
        }
        (sz0, sz1, sz2) = (_sz0, _sz1, _sz2);
        sz01 = sz0 * sz1;
        var target_set = new SortedSet<int>(new TargetComparer(sz0, sz1, sz2, root));
        var searchSet = new HashSet<int>();
        Heap<HeapElemX> heap = new();
        var connection = new Dictionary<int, int>();
        var elems = new Dictionary<int, HeapElemX>();

        target_set.Add(targetIndex);
        HeapElemX branchElem = new(-1,-1);
        int iteration = 0;
        int branchIndex = -1;
        var addResults = new HashSet<int>();
        while (target_set.Count > 0)
        {
            Debug.Log("iteration:" + iteration++);
            

            int target_index = target_set.Last();
            Debug.Log("target_index：" + target_index);
            createSphere(IndexToVector(target_index), new Vector3Int(sz0, sz1, sz2), Color.cyan, 0.05f);

            //int target_index = target_set.First();
            target_set.Remove(target_index);
            HashSet<Vector3> voxelSet = findSubVoxels(target_index, gwdt, searchSet, target_set, results, sz0, sz1, sz2, bkg_thresh);
            Debug.Log("voxelSetCount:" + voxelSet.Count);
            if (voxelSet.Count < 3) continue;

            //trace back to trunk
            SearchCluster(voxelSet, branchElem, -1, root, new Vector3Int(sz0, sz1, sz2), new Vector3Int(o_width, o_height, o_depth), searchSet, results, addResults,target_set, heap, elems, connection, bkg_thresh);

            (Vector3 direction, Vector3 maximum_pos, Vector3 minimum_pos) = PCA(voxelSet, new Vector3Int(sz0, sz1, sz2), new Vector3Int(o_width, o_height, o_depth));
            int serachLength = 5;
            SearchCluster(minimum_pos, -direction, serachLength, gwdt, searchSet, results, target_set, bkg_thresh);
            SearchCluster(maximum_pos, direction, serachLength, gwdt, searchSet, results, target_set, bkg_thresh);
            Debug.Log("target_set count befor heap:"+target_set.Count);

            if (branchIndex == -1)
            {
                branchIndex = heap.elems[0].img_index;
                Debug.Log("branchIndex:" + branchIndex);
            }

            while (!heap.empty())
            {
                HeapElemX min_elem = heap.delete_min();
                elems.Remove(min_elem.img_index);
                addResults.Add(min_elem.img_index);

                //insert target
                if (target_set.Contains(min_elem.img_index)) target_set.Remove(min_elem.img_index);


                int min_index = min_elem.img_index;
                Debug.Log(min_index);
                parent[min_index] = min_elem.prev_index;
                state[min_index] = States.ALIVE;

                int i = (int)(min_index % sz0);
                int j = (int)((min_index / sz0) % sz1);
                int k = (int)((min_index / sz01) % sz2);

                int w, h, d;
                for (int ii = -1; ii <= 1; ii++)
                {
                    w = i + ii;
                    if (w < 0 || w >= sz0) continue;
                    for (int jj = -1; jj <= 1; jj++)
                    {
                        h = j + jj;
                        if (h < 0 || h >= sz1) continue;
                        for (int kk = -1; kk <= 1; kk++)
                        {
                            d = k + kk;
                            if (d < 0 || d >= sz2) continue;
                            int offset = Math.Abs(ii) + Math.Abs(jj) + Math.Abs(kk);
                            if (offset == 0 || offset > cnn_type) continue;
                            double factor = (offset == 1) ? 1.0 : ((offset == 2) ? 1.414214 : ((offset == 3) ? 1.732051 : 0.0));
                            int index = d * sz01 + h * sz0 + w;
                            int marker_distance = (int)Vector3.Distance(root.position, new Vector3(w, h, d));
                            //double true_thresh;
                            //true_thresh = marker_distance <= 50 ? higher_thresh : bkg_thresh;
                            if (is_break_accept)
                            {

                                if (gwdt[index] < bkg_thresh && gwdt[min_index] < bkg_thresh) continue;
                            }
                            else
                            {
                                if (gwdt[index] < bkg_thresh) continue;
                            }
                            if (state[index] != States.ALIVE)
                            {
                                float new_dist = (float)(phi[min_index] + (GI(gwdt[index]) + GI(gwdt[min_index])) * factor * 0.5);
                                int prev_index = min_index;

                                if (state[index] == States.FAR)
                                {
                                    phi[index] = new_dist;
                                    HeapElemX elem = new HeapElemX(index, phi[index]);
                                    elem.prev_index = prev_index;
                                    heap.insert(elem);
                                    elems[index] = elem;
                                    state[index] = States.TRIAL;
                                }
                                else if (state[index] == States.TRIAL)
                                {
                                    if (phi[index] > new_dist)
                                    {
                                        phi[index] = new_dist;
                                        HeapElemX elem = elems[index];
                                        heap.adjust(elem.heap_id, phi[index]);
                                        elem.prev_index = prev_index;
                                    }
                                }
                            }
                        }
                    }
                }

                if (connection.ContainsKey(min_index))
                {
                    //Debug.Log("connection works");
                    int index = connection[min_index];
                    w = (int)(min_index % sz0);
                    h = (int)((min_index / sz0) % sz1);
                    d = (int)((min_index / sz01) % sz2);
                    double factor = Vector3.Distance(new Vector3(i, j, k), new Vector3(w, h, d));
                    if (state[index] != States.ALIVE)
                    {
                        float new_dist = (float)(phi[min_index] + (GI(gwdt[index]) + GI(gwdt[min_index])) * factor * 0.5);
                        int prev_index = min_index;

                        if (state[index] == States.FAR)
                        {
                            phi[index] = new_dist;
                            HeapElemX elem = new HeapElemX(index, phi[index]);
                            elem.prev_index = prev_index;
                            heap.insert(elem);
                            elems[index] = elem;
                            state[index] = States.TRIAL;
                        }
                        else if (state[index] == States.TRIAL)
                        {
                            if (phi[index] > new_dist)
                            {
                                phi[index] = new_dist;
                                HeapElemX elem = elems[index];
                                heap.adjust(elem.heap_id, phi[index]);
                                elem.prev_index = prev_index;
                            }
                        }
                    }
                }
            }
            branchElem = new HeapElemX(-1, -1);

            Debug.Log("target_set count befor heap:" + target_set.Count);
        }

        Debug.Log("==========done" + heap.elems.Count);

        //Texture3D texture3D = new Texture3D(sz0, sz1, sz2, TextureFormat.R8, false);
        //texture3D.wrapMode = TextureWrapMode.Clamp;
        //for (int i = 0; i < gsdt.Length; i++)
        //{
        //    //gsdt[i] = (float)(gsdt[i] / maximum);
        //    if (gsdt_float[i] == 255) gsdt_float[i] = 255;
        //    else if (state[i] == States.ALIVE && gsdt_float[i] == 0)
        //    {
        //        gsdt_float[i] = 255;
        //    }
        //    else gsdt_float[i] = 0;
        //}
        //texture3D.SetPixelData(gsdt_float, 0);
        //texture3D.Apply();
        //AssetDatabase.DeleteAsset("Assets/Textures/" + "after" + ".Asset");
        //AssetDatabase.CreateAsset(texture3D, "Assets/Textures/" + "after" + ".Asset");
        //AssetDatabase.SaveAssets();
        //AssetDatabase.Refresh();

        //save swc tree
        Debug.Log("addResults count:" + addResults.Count);
        Debug.Log("Results count:" + results.Count);

        HashSet<int> trunkSet = new HashSet<int>();
        foreach (Marker marker in filteredTree)
        {
            trunkSet.Add(VectorToIndex(marker.position));
        }

        var branch = new List<Marker>();
        foreach (var index in addResults)
        {
            if (state[index] != States.ALIVE) continue;
            int i = (int)(index % sz0);
            int j = (int)((index / sz0) % sz1);
            int k = (int)((index / sz01) % sz2);
            Marker marker = new Marker(new Vector3(i, j, k));
            markers[index] = marker;
            branch.Add(marker);
        }

        foreach (var index in addResults)
        {
            if (state[index] != States.ALIVE) continue;
            int parentIndex = parent[index];
            Marker marker = markers[index];
            Marker parentMarker = markers[parentIndex];
            marker.parent = parentMarker;
        }

        results.UnionWith(addResults);

        Debug.Log("branch count:" + branch.Count);

        branchRoot = new Marker();
        if (branch.Count > 0)
        {
            int tempindex = branchIndex;
            while (!trunkSet.Contains(tempindex))
            {
                tempindex = parent[tempindex];
                Marker marker = markers[tempindex];
                branch.Add(marker);
            }
            branchRoot = markers[tempindex];

        }

        Debug.Log("branch count:" + branch.Count);
        Debug.Log("Results count:" + results.Count);
        Debug.Log("repair done");
        return branch;
    }

    public HashSet<uint> TraceTarget(int targetIndex)
    {
        HashSet<uint> branchSet = new();
        int iter = targetIndex;
        while (!results.Contains(iter))
        {
            branchSet.Add((uint)iter);
            iter = parent[iter];
        }

        return branchSet;
    }


    public double GI(double intensity)
    {
        double lamda = 10;
        double ret = Math.Exp(lamda * (1 - intensity / max_intensity) * (1 - intensity / max_intensity));
        return ret;
    }

    private HashSet<Vector3> findSubVoxels(int index, float[] gwdt, HashSet<int> searchSet, SortedSet<int> targetSet, HashSet<int> results, int sz0, int sz1, int sz2, int bkg_thresh = 30)
    {
        int sz01 = sz0 * sz1;
        HashSet<Vector3> tmpClt = new HashSet<Vector3>();
        Queue<Vector3Int> voxelList = new();
        int x = index % sz0;
        int y = (index / sz0) % sz1;
        int z = (index / sz01) % sz2;
        Vector3Int tmpVox = new(x, y, z);
        voxelList.Enqueue(tmpVox);
        int offset = 1;
        while (voxelList.Count != 0 && voxelList.Count < 1000)
        {
            tmpVox = voxelList.Dequeue();
            tmpClt.Add(tmpVox);

            for (int i = tmpVox.x - offset; i <= tmpVox.x + offset; i++)
            {
                if (i < 0 || i >= sz0) continue;
                for (int j = tmpVox.y - offset; j <= tmpVox.y + offset; j++)
                {
                    if (j < 0 || j >= sz1) continue;
                    for (int k = tmpVox.z - offset; k <= tmpVox.z + offset; k++)
                    {
                        if (k < 0 || k >= sz2) continue;
                        // append this voxel
                        int tmp_index = k * sz01 + j * sz0 + i;
                        //Debug.Log("search:" + searchSet.Contains(tmp_index));
                        //Debug.Log("result:" + results.Contains(tmp_index));
                        //Debug.Log(gwdt[tmp_index]);
                        if (gwdt[tmp_index] >= bkg_thresh && !searchSet.Contains(tmp_index) && !results.Contains(tmp_index))
                        {
                            searchSet.Add(tmp_index);
                            voxelList.Enqueue(new Vector3Int(i, j, k));
                            targetSet.Remove(tmp_index);
                        }
                    }
                }
            }
        }
        return tmpClt;
    }

    private (Vector3, Vector3, Vector3) PCA(HashSet<Vector3> voxelSet, Vector3Int vDim, Vector3Int oDim)
    {
        Vector3 average = Vector3.zero;
        List<Vector3> subVoxels = new List<Vector3>();

        foreach (var subvoxel in voxelSet)
        {
            average += subvoxel;
            int tmp_index = (int)(subvoxel.z * sz01 + subvoxel.y * sz0 + subvoxel.x);
        }
        average /= voxelSet.Count;

        foreach (var subvoxel in voxelSet)
        {
            var temp = subvoxel - average;
            subVoxels.Add(temp);
        }

        var M = new DenseMatrix(3, subVoxels.Count);

        for (int i = 0; i < subVoxels.Count; i++)
        {
            M[0, i] = subVoxels[i].x;
            M[1, i] = subVoxels[i].y;
            M[2, i] = subVoxels[i].z;
        }

        var c = (1.0 / subVoxels.Count) * M * M.Transpose();
        var cc = c.Evd();

        var eigenValues = cc.EigenValues;
        var eigenVectors = cc.EigenVectors;

        var val0 = Math.Abs(eigenValues[0].Real);
        var val1 = Math.Abs(eigenValues[1].Real);
        var val2 = Math.Abs(eigenValues[2].Real);

        var vec0 = eigenVectors.Column(0);
        var vec1 = eigenVectors.Column(1);
        var vec2 = eigenVectors.Column(2);

        var vec = vec0;
        if (val0 > val1 && val0 > val2) vec = vec0;
        else if (val1 > val0 && val1 > val2) vec = vec1;
        else vec = vec2;

        Vector3 direction = new Vector3((float)vec[0], (float)vec[1], (float)vec[2]).normalized;
        Vector3 parentDirection = GetDirection(average, vDim, oDim);
        if (Vector3.Dot(direction, parentDirection) < 0) direction = -direction;
        Vector3 position = new Vector3(average.x / sz0, average.y / sz1, average.z / sz2) - new Vector3(0.5f, 0.5f, 0.5f);
        //Debug.Log(direction + " " + position);
        //var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        //var trans = GameObject.Find("Cube").transform;
        //sphere.transform.position = trans.TransformPoint(position);
        //sphere.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
        //sphere.transform.parent = GameObject.Find("SerachPoints").transform;
        //Debug.DrawLine(sphere.transform.position, sphere.transform.position + trans.TransformDirection(direction) * 0.25f, Color.yellow, 1000);

        //计算点在主方向上一维投影坐标
        List<float> projections = new List<float>();
        foreach (var subVoxel in subVoxels)
        {
            projections.Add(direction.x * subVoxel.x + direction.y * subVoxel.y + direction.z * subVoxel.z);
        }

        float maximum = float.MinValue, minimum = float.MaxValue;
        int max_i = 0, min_i = 0;
        for (int i = 0; i < projections.Count; i++)
        {
            if (projections[i] > maximum)
            {
                maximum = Math.Max(maximum, projections[i]);
                max_i = i;
            }
            if (projections[i] < minimum)
            {
                minimum = Math.Min(minimum, projections[i]);
                min_i = i;
            }

        }

        Vector3 maximum_pos = subVoxels[max_i] + average;
        Vector3 minimum_pos = subVoxels[min_i] + average;

        int maximum_index = (int)(maximum_pos.z * sz01 + maximum_pos.y * sz0 + maximum_pos.x);
        int minimum_index = (int)(minimum_pos.z * sz01 + minimum_pos.y * sz0 + minimum_pos.x);
        return (direction, maximum_pos, minimum_pos);
    }

    //pca
    private void SearchCluster(Vector3 baseVoxel, Vector3 direction, int searchLength, float[] gsdt, HashSet<int> searchSet, HashSet<int> results, SortedSet<int> target_set, int bkg_thresh)
    {
        Vector3 a = Vector3.Cross(Vector3.forward, direction);
        if (a == Vector3.zero)
        {
            a = Vector3.Cross(Vector3.up, direction).normalized;
        }
        Vector3 b = Vector3.Cross(a, direction).normalized;

        int sz01 = sz0 * sz1;
        Vector3 circleCenter = baseVoxel;
        bool is_break = false;
        for (int length = 1; length < searchLength && !is_break; length++)
        {
            circleCenter += direction;
            int radius = (int)Math.Round(length * Math.Tan(Math.PI / 6));

            for (int r = 1; r <= radius && !is_break; r++)
            {
                for (float theta = 0; theta < 2 * Mathf.PI; theta += Mathf.PI / 36)
                {
                    Vector3 tmp = circleCenter + r * (Mathf.Cos(theta) * a + Mathf.Sin(theta) * b);
                    tmp = new Vector3(Mathf.Round(tmp.x), Mathf.Round(tmp.y), Mathf.Round(tmp.z));
                    if (tmp.x >= 0 && tmp.x < sz0 && tmp.y >= 0 && tmp.y < sz1 && tmp.z >= 0 && tmp.z < sz2)
                    {
                        int tmp_index = (int)(tmp.z * sz01 + tmp.y * sz0 + tmp.x);
                        if (gsdt[tmp_index] >= bkg_thresh)
                        {
                            int base_index = (int)(baseVoxel.z * sz01 + baseVoxel.y * sz0 + baseVoxel.x);

                            //接入重建主结构
                            if (!searchSet.Contains(tmp_index) && !results.Contains(tmp_index))
                            {
                                //var tmpVoxels = findSubVoxels(tmp_index, results, sz0, sz1, sz2);
                                //if (tmpVoxels.Count < 10) continue;
                                target_set.Add(tmp_index);
                                createSphere(IndexToVector(tmp_index), new Vector3Int(sz0, sz1, sz2), Color.cyan, 0.02f);
                                //is_break = true;
                            }
                            //连接非连通区域
                        }
                    }
                }
            }
        }
    }


    private (Vector3, Vector3, Vector3) ParentDir(HashSet<Vector3> voxelSet, int sz01, int sz0, Vector3Int volumeDim, Vector3Int occupancyDim)
    {
        (int v_width, int v_height, int v_depth) = (volumeDim.x, volumeDim.y, volumeDim.z);
        (int o_width, int o_height, int o_depth) = (occupancyDim.x, occupancyDim.y, occupancyDim.z);
        Vector3 average = Vector3.zero;
        List<Vector3> subVoxels = new List<Vector3>();


        foreach (var subvoxel in voxelSet)
        {
            average += subvoxel;
            int tmp_index = (int)(subvoxel.z * sz01 + subvoxel.y * sz0 + subvoxel.x);
        }
        average /= voxelSet.Count;
        foreach (var subvoxel in voxelSet)
        {
            var temp = subvoxel - average;
            subVoxels.Add(temp);
        }
        average.x = average.x / v_width * o_width;
        average.y = average.y / v_height * o_height;
        average.z = average.z / v_depth * o_depth;
        int index_oc = ((int)average.x + (int)average.y * o_width + (int)average.z * o_width * o_height);
        int parent_index_oc = parent_oc[index_oc];
        int pp_index_oc = parent_oc[parent_index_oc];
        //Debug.Log(index_oc + " " + parent_index_oc + " " + pp_index_oc);

        Vector3 parent_pos = new Vector3(parent_index_oc % o_width, (parent_index_oc / o_width) % o_height, (parent_index_oc / o_width / o_height) % o_depth);
        Vector3 pparent_pos = new Vector3(pp_index_oc % o_width, (pp_index_oc / o_width) % o_height, (pp_index_oc / o_width / o_height) % o_depth);

        Vector3 direction = (parent_pos - average).normalized;
        Vector3 direction2 = ((pparent_pos - parent_pos) / 2 + (parent_pos - average) / 2).normalized;
        direction2 = (pparent_pos - average).normalized;
        Vector3 position = new Vector3(average.x / o_width, average.y / o_height, average.z / o_depth) - new Vector3(0.5f, 0.5f, 0.5f);
        //Debug.Log(direction + " " + position);
        //var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        //var trans = GameObject.Find("Cube").transform;
        //sphere.transform.position = trans.TransformPoint(position);
        //sphere.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
        //sphere.transform.parent = GameObject.Find("SerachPoints").transform;

        //Debug.DrawLine(sphere.transform.position, sphere.transform.position + trans.TransformDirection(direction) * 0.25f, Color.blue, 1000);
        //Debug.DrawLine(sphere.transform.position, sphere.transform.position + trans.TransformDirection(direction2) * 0.25f, Color.green, 1000);

        List<float> projections = new List<float>();
        foreach (var subVoxel in subVoxels)
        {
            projections.Add(direction.x * subVoxel.x + direction.y * subVoxel.y + direction.z * subVoxel.z);
        }
        //Debug.Log(projections.Count);
        average = new Vector3(average.x / o_width * v_width, average.y / o_height * v_height, average.z / o_depth * v_depth);
        float maximum = float.MinValue, minimum = float.MaxValue;
        int max_i = 0, min_i = 0;
        for (int i = 0; i < projections.Count; i++)
        {
            if (projections[i] > maximum)
            {
                maximum = Math.Max(maximum, projections[i]);
                max_i = i;
            }
            if (projections[i] < minimum)
            {
                minimum = Math.Min(minimum, projections[i]);
                min_i = i;
            }

        }
        //Debug.Log(max_i + " " + min_i);
        Vector3 maximum_pos = subVoxels[max_i] + average;
        Vector3 minimum_pos = subVoxels[min_i] + average;

        int maximum_index = (int)(maximum_pos.z * sz01 + maximum_pos.y * sz0 + maximum_pos.x);
        int minimum_index = (int)(minimum_pos.z * sz01 + minimum_pos.y * sz0 + minimum_pos.x);
        return (direction2, maximum_pos, minimum_pos);
    }

    private void SearchCluster(HashSet<Vector3> voxelSet, HeapElemX elem, int sourceIndex, Marker root, Vector3Int vDim, Vector3Int oDim, HashSet<int> searchSet, HashSet<int> results, HashSet<int> addResults,SortedSet<int> target_set, Heap<HeapElemX> heap, Dictionary<int, HeapElemX> elems, Dictionary<int, int> connection, int bkg_thresh)
    {
        int blockSize = vDim.x / oDim.x;
        Vector3 baseVoxel = Vector3.zero;
        Vector3 baseVoxel2 = Vector3.zero;
        float minDistance = float.MaxValue;
        float maxDistance = 0;
        Vector3 average = Vector3.zero;
        foreach (var voxel in voxelSet)
        {
            average += voxel;
        }
        average = average / voxelSet.Count;
        Vector3 direction = GetDirection(average, vDim, oDim);
        foreach (var voxel in voxelSet)
        {
            //createSphere(voxel, new Vector3Int(2048, 2048, 140), Color.green,0.005f);
            float distance = Vector3.Distance(voxel, root.position);
            //float distance = Vector3.Dot(voxel - average, direction);
            if (distance < minDistance)
            {
                minDistance = distance;
                baseVoxel = voxel;
            }
            if (distance > maxDistance)
            {
                maxDistance = distance;
                baseVoxel2 = voxel;
            }
        }
        int baseIndex = VectorToIndex(baseVoxel);
        int baseIndex2 = VectorToIndex(baseVoxel2);
        if (sourceIndex != -1)
        {
            connection[baseIndex2] = sourceIndex;
            connection[sourceIndex] = baseIndex2;
        }
        //Debug.Log(baseVoxel);
        Vector3 occupancyBaseVoxel = Vector3.zero;
        createSphere(baseVoxel, vDim, Color.yellow);
        occupancyBaseVoxel.x = baseVoxel.x / vDim.x * oDim.x;
        occupancyBaseVoxel.y = baseVoxel.y / vDim.y * oDim.y;
        occupancyBaseVoxel.z = baseVoxel.z / vDim.z * oDim.z;
        //Debug.Log(occupancyBaseVoxel);
        //createSphere(occupancyBaseVoxel, oDim, Color.blue);
        int index_oc = ((int)occupancyBaseVoxel.x + (int)occupancyBaseVoxel.y * oDim.x + (int)occupancyBaseVoxel.z * oDim.y * oDim.x);

        int parentOccupancyIndex = parent_oc[index_oc];
        Vector3 parentOccupancyPos = IndexToVector(parentOccupancyIndex, oDim);
        Vector3 parentBasePos = Vector3Int.zero;
        parentBasePos.x = (int)(parentOccupancyPos.x / oDim.x * vDim.x);
        parentBasePos.y = (int)(parentOccupancyPos.y / oDim.y * vDim.y);
        parentBasePos.z = (int)(parentOccupancyPos.z / oDim.z * vDim.z);
        createSphere(parentBasePos, vDim, Color.blue);
        int searchTimes = 0;
        bool is_break = false;
        while (searchTimes < 5 && !is_break)
        {
            for (int i = -2; i < blockSize && !is_break; i++)
            {
                for (int j = -2; j < blockSize && !is_break; j++)
                {
                    for (int k = -2; k < blockSize && !is_break; k++)
                    {
                        Vector3 tmp = parentBasePos + new Vector3Int(i, j, k);
                        if (tmp.x >= 0 && tmp.x < sz0 && tmp.y >= 0 && tmp.y < sz1 && tmp.z >= 0 && tmp.z < sz2)
                        {
                            int tmpIndex = VectorToIndex(tmp);
                            if (gwdt[tmpIndex] >= bkg_thresh)
                            {
                                if (elem.img_index == -1 && (results.Contains(tmpIndex)||addResults.Contains(tmpIndex)))
                                {
                                    Debug.Log("find main construction");
                                    double factor = Vector3.Distance(tmp, baseVoxel);
                                    float new_dist = (float)(phi[tmpIndex] + (GI(gwdt[tmpIndex]) + GI(gwdt[baseIndex])) * factor * 0.5);
                                    phi[baseIndex] = new_dist;
                                    elem.img_index = baseIndex;
                                    elem.value = phi[baseIndex];
                                    elem.prev_index = tmpIndex;
                                    heap.insert(elem);
                                    elems[baseIndex] = elem;
                                    //state[maximum_index] = States.TRIAL;
                                    is_break = true;
                                }
                                else if (!searchSet.Contains(tmpIndex))
                                {
                                    //var tmpVoxels = findSubVoxels(tmp_index, results, sz0, sz1, sz2);
                                    //if (tmpVoxels.Count < 10) continue;
                                    //target_set.Add(tmpIndex);
                                    HashSet<Vector3> tmpVoxelSet = findSubVoxels(tmpIndex, gwdt, searchSet, target_set,results, sz0, sz1, sz2, bkg_thresh);
                                    if (voxelSet.Count < 3) continue;
                                    SearchCluster(tmpVoxelSet, elem, baseIndex, root, vDim, oDim, searchSet, results, addResults,target_set, heap, elems, connection, bkg_thresh);
                                    is_break = true;
                                }

                            }
                        }
                    }
                }
            }
            if (is_break) break;
            searchTimes++;
            parentOccupancyIndex = parent_oc[parentOccupancyIndex];
            parentOccupancyPos = IndexToVector(parentOccupancyIndex, oDim);
            parentBasePos.x = (int)(parentOccupancyPos.x / oDim.x * vDim.x);
            parentBasePos.y = (int)(parentOccupancyPos.y / oDim.y * vDim.y);
            parentBasePos.z = (int)(parentOccupancyPos.z / oDim.z * vDim.z);
            createSphere(parentBasePos, vDim, Color.blue);
            //Debug.Log(occupancyBaseVoxel);
        }


    }

    private void SearchCluster(HashSet<Vector3> voxelSet, int sourceIndex, Marker root, Vector3Int vDim, Vector3Int oDim, HashSet<int> searchSet, HashSet<int> results, SortedSet<int> target_set, Heap<HeapElemX> heap, Dictionary<int, HeapElemX> elems, Dictionary<int, int> connection, int bkg_thresh)
    {
        int blockSize = vDim.x / oDim.x;
        Debug.Log(blockSize);
        Vector3 baseVoxel = Vector3.zero;
        Vector3 baseVoxel2 = Vector3.zero;
        float minDistance = float.MaxValue;
        float maxDistance = 0;
        Vector3 average = Vector3.zero;
        foreach (var voxel in voxelSet)
        {
            average += voxel;
        }
        average = average / voxelSet.Count;
        Vector3 direction = GetDirection(average, vDim, oDim);
        foreach (var voxel in voxelSet)
        {
            //createSphere(voxel, new Vector3Int(2048, 2048, 140), Color.green,0.005f);
            float distance = Vector3.Distance(voxel, root.position);
            //float distance = Vector3.Dot(voxel - average, direction);
            if (distance < minDistance)
            {
                minDistance = distance;
                baseVoxel = voxel;
            }
            if (distance > maxDistance)
            {
                maxDistance = distance;
                baseVoxel2 = voxel;
            }
        }
        int baseIndex = VectorToIndex(baseVoxel);
        int baseIndex2 = VectorToIndex(baseVoxel2);
        if (sourceIndex != -1)
        {
            connection[baseIndex2] = sourceIndex;
            connection[sourceIndex] = baseIndex2;
        }
        //Debug.Log(baseVoxel);
        Vector3 occupancyBaseVoxel = Vector3.zero;
        createSphere(baseVoxel, vDim, Color.yellow);
        occupancyBaseVoxel.x = baseVoxel.x / vDim.x * oDim.x;
        occupancyBaseVoxel.y = baseVoxel.y / vDim.y * oDim.y;
        occupancyBaseVoxel.z = baseVoxel.z / vDim.z * oDim.z;
        //Debug.Log(occupancyBaseVoxel);
        //createSphere(occupancyBaseVoxel, oDim, Color.blue);
        int index_oc = ((int)occupancyBaseVoxel.x + (int)occupancyBaseVoxel.y * oDim.x + (int)occupancyBaseVoxel.z * oDim.y * oDim.x);

        int parentOccupancyIndex = parent_oc[index_oc];
        Vector3 parentOccupancyPos = IndexToVector(parentOccupancyIndex, oDim);
        Vector3 parentBasePos = Vector3Int.zero;
        parentBasePos.x = (int)(parentOccupancyPos.x / oDim.x * vDim.x);
        parentBasePos.y = (int)(parentOccupancyPos.y / oDim.y * vDim.y);
        parentBasePos.z = (int)(parentOccupancyPos.z / oDim.z * vDim.z);
        createSphere(parentBasePos, vDim, Color.blue);
        int searchTimes = 0;
        bool is_break = false;
        Debug.Log("searchset count:"+searchSet.Count);
        while (searchTimes < 10 && !is_break)
        {
            for (int i = -2; i < blockSize && !is_break; i++)
            {
                for (int j = -2; j < blockSize && !is_break; j++)
                {
                    for (int k = -2; k < blockSize && !is_break; k++)
                    {
                        Vector3 tmp = parentBasePos + new Vector3Int(i, j, k);
                        if (tmp.x >= 0 && tmp.x < sz0 && tmp.y >= 0 && tmp.y < sz1 && tmp.z >= 0 && tmp.z < sz2)
                        {
                            int tmpIndex = VectorToIndex(tmp);
                            if (gwdt[tmpIndex] >= bkg_thresh)
                            {
                                Debug.Log("??????");
                                if (results.Contains(tmpIndex))
                                {
                                    Debug.Log("find main construction");
                                    double factor = Vector3.Distance(tmp, baseVoxel);
                                    float new_dist = (float)(phi[tmpIndex] + (GI(gwdt[tmpIndex]) + GI(gwdt[baseIndex])) * factor * 0.5);
                                    phi[baseIndex] = new_dist;
                                    HeapElemX elem = new HeapElemX(baseIndex, phi[baseIndex]);
                                    elem.img_index = baseIndex;
                                    elem.value = phi[baseIndex];
                                    elem.prev_index = tmpIndex;
                                    heap.insert(elem);
                                    elems[baseIndex] = elem;
                                    //state[maximum_index] = States.TRIAL;
                                    is_break = true;
                                }
                                else if (!searchSet.Contains(tmpIndex))
                                {
                                    //var tmpVoxels = findSubVoxels(tmp_index, results, sz0 , sz1, sz2);
                                    //if (tmpVoxels.Count < 10) continue;
                                    //target_set.Add(tmpIndex);
                                    HashSet<Vector3> tmpVoxelSet = findSubVoxels(tmpIndex, gwdt, searchSet, target_set, results, sz0, sz1, sz2, bkg_thresh);
                                    if (voxelSet.Count < 3) continue;
                                    createSphere(IndexToVector(tmpIndex), vDim, Color.white,0.1f);
                                    SearchCluster(tmpVoxelSet,baseIndex, root, vDim, oDim, searchSet, results, target_set, heap, elems, connection, bkg_thresh);
                                    is_break = true;
                                }
                                 
                            }
                        }
                    }
                }
            }
            if (is_break) break;
            searchTimes++;
            parentOccupancyIndex = parent_oc[parentOccupancyIndex];
            parentOccupancyPos = IndexToVector(parentOccupancyIndex, oDim);
            parentBasePos.x = (int)(parentOccupancyPos.x / oDim.x * vDim.x);
            parentBasePos.y = (int)(parentOccupancyPos.y / oDim.y * vDim.y);
            parentBasePos.z = (int)(parentOccupancyPos.z / oDim.z * vDim.z);
            createSphere(parentBasePos, vDim, Color.blue);
            //Debug.Log(occupancyBaseVoxel);
        }


    }

    private Vector3 GetDirection(Vector3 pos, Vector3Int vDim, Vector3Int oDim)
    {
        int blockSize = vDim.x / oDim.x;
        Vector3 baseVoxel = Vector3.zero;
        Vector3 baseVoxel2 = Vector3.zero;

        int baseIndex = VectorToIndex(pos);

        //Debug.Log(baseVoxel);
        Vector3 occupancyBaseVoxel = Vector3.zero;
        createSphere(baseVoxel, vDim, Color.yellow);
        occupancyBaseVoxel.x = baseVoxel.x / vDim.x * oDim.x;
        occupancyBaseVoxel.y = baseVoxel.y / vDim.y * oDim.y;
        occupancyBaseVoxel.z = baseVoxel.z / vDim.z * oDim.z;
        //Debug.Log(occupancyBaseVoxel);
        createSphere(occupancyBaseVoxel, oDim, Color.blue);
        int index_oc = ((int)occupancyBaseVoxel.x + (int)occupancyBaseVoxel.y * oDim.x + (int)occupancyBaseVoxel.z * oDim.y * oDim.x);

        int parentOccupancyIndex = parent_oc[index_oc];
        Vector3 parentOccupancyPos = IndexToVector(parentOccupancyIndex, oDim);
        Vector3 parentBasePos = Vector3Int.zero;
        parentBasePos.x = (int)(parentOccupancyPos.x / oDim.x * vDim.x);
        parentBasePos.y = (int)(parentOccupancyPos.y / oDim.y * vDim.y);
        parentBasePos.z = (int)(parentOccupancyPos.z / oDim.z * vDim.z);
        int searchTimes = 0;

        while (searchTimes < 5)
        {
            searchTimes++;
            parentOccupancyIndex = parent_oc[parentOccupancyIndex];
            parentOccupancyPos = IndexToVector(parentOccupancyIndex, oDim);
            parentBasePos.x = (int)(parentOccupancyPos.x / oDim.x * vDim.x);
            parentBasePos.y = (int)(parentOccupancyPos.y / oDim.y * vDim.y);
            parentBasePos.z = (int)(parentOccupancyPos.z / oDim.z * vDim.z);
            //Debug.Log(occupancyBaseVoxel);
        }
        Vector3 tmp = parentBasePos + new Vector3Int(blockSize / 2, blockSize / 2, blockSize / 2);
        return (tmp - pos).normalized;
    }


    private void createSphere(Vector3 pos, Vector3Int Dim, Color color, float scale = 0.01f)
    {
        //(int width, int height, int depth) = (Dim.x, Dim.y, Dim.z);
        //Vector3 position = new Vector3(pos.x / width, pos.y / height, pos.z / depth) - new Vector3(0.5f, 0.5f, 0.5f);
        //var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        //var trans = GameObject.Find("Cube").transform;
        //sphere.transform.position = trans.TransformPoint(position);
        //sphere.transform.localScale = new Vector3(Scale, Scale, Scale);
        //sphere.transform.parent = GameObject.Find("SerachPoints").transform;
        //sphere.GetComponent<MeshRenderer>().material.color = color;
    }

    private Vector3Int IndexToVector(int index)
    {
        int x = (int)(index % sz0);
        int y = (int)((index / sz0) % sz1);
        int z = (int)((index / sz01) % sz2);
        return new Vector3Int(x, y, z);
    }

    private Vector3 IndexToVector(int index, Vector3Int dim)
    {
        int x = (int)(index % dim.x);
        int y = (int)((index / dim.x) % dim.y);
        int z = (int)((index / dim.x / dim.y) % dim.z);
        return new Vector3(x, y, z);
    }

    private int VectorToIndex(Vector3 pos)
    {
        int index = (int)((int)pos.x + (int)pos.y * sz0 + (int)pos.z * sz0 * sz1);
        return index;
    }

    private int VectorToIndex(Vector3 pos, Vector3Int dim)
    {
        int index = (int)((int)pos.x + (int)pos.y * dim.x + (int)pos.z * dim.x * dim.y);
        return index;
    }
}

