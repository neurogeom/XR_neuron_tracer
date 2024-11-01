using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeapElem
{
    public int heap_id;
    public int img_index;
    public double value;
    private int index;
    private float new_dist;

    public HeapElem(int _index, double _value)
    {
        img_index = _index;
        value = _value;
        heap_id = -1;
    }

    public HeapElem(int index, float new_dist)
    {
        this.index = index;
        this.new_dist = new_dist;
    }
}

public class HeapElemX : HeapElem
{
    public int prev_index;
    public HeapElemX(int _index, double _value) : base(_index, _value)
    {
        prev_index = -1;
    }
}

public class HeapElemXX : HeapElem
{
    Marker parent_marker;
    public HeapElemXX(int _index, double _value, Marker _parent_marker) : base(_index, _value)
    {
        parent_marker = _parent_marker;
    }
}
public class Heap<T> where T : HeapElem
{
    public List<T> elems;
    
    public Heap()
    {
        elems = new List<T>();
    }
    public void insert(T elem)
    {
        elems.Add(elem);
        elem.heap_id = elems.Count - 1;
        up_heap(elem.heap_id);
    }

    public void clear()
    {
        elems.Capacity = 0;
    }

    public T delete_min()
    {
        if (elems.Count == 0) return null;
        T min_elem = elems[0];

        if (elems.Count == 1) elems.Clear();
        else
        {
            elems[0] = elems[elems.Count - 1];
            elems[0].heap_id = 0;
            elems.RemoveAt(elems.Count - 1);
            down_heap(0);
        }
        return min_elem;
    }

    public bool empty()
    { 
        return elems.Count==0; 
    }
    public void adjust(int id, double new_value)
    {
        double old_value = elems[id].value;
        elems[id].value = new_value;
        if (new_value < old_value) up_heap(id);
        else if (new_value > old_value) down_heap(id);
    }

    private void down_heap(int id)
    {
        int cid1 = 2 * (id + 1) - 1;
        int cid2 = 2 * (id + 1);
        if (cid1 >= elems.Count) return;
        else if (cid1 == elems.Count - 1)
        {
            swap_heap(id, cid1);
        }
        else if (cid1 < elems.Count - 1)
        {
            int cid = elems[cid1].value < elems[cid2].value ? cid1 : cid2;
            if (swap_heap(id, cid)) down_heap(cid);
        }
    }

    private void up_heap(int id)
    {
        int pid = (id + 1) / 2 - 1;
        if (swap_heap(id, pid)) up_heap(pid);
    }

    private bool swap_heap(int id1, int id2)
    {
        if (id1 < 0 || id1 >= elems.Count || id2 < 0 || id2 > elems.Count) return false;
        if (id1 == id2) return false;
        int pid = id1 < id2 ? id1 : id2;
        int cid = id1 > id2 ? id1 : id2;
        if (elems[pid].value <= elems[cid].value) return false;
        else
        {
            T tmp = elems[pid];
            elems[pid] = elems[cid];
            elems[cid] = tmp;
            elems[pid].heap_id = pid;
            elems[cid].heap_id = cid;
            return true;
        }
    }
}
