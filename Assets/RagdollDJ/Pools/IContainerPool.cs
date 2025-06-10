using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Container Pool Base Interface
/// </summary>
public interface IContainerPoolBase
{
    void Init(int defaultSize);
    void Trim();
    int Size();
    void Clear();
}


/// <summary>
/// Container Pool Item Interface
/// </summary>
public interface IContainerPoolItem<T>: IContainerPoolBase
{
    bool IsUsing();
    bool Get(out ICollection<T> value);
    void Remove();
}


/// <summary>
/// Container Pool Interface
/// </summary>
public interface IContainerPool: IContainerPoolBase
{
    bool Get<T>(out ICollection<T> value,int defaultSize = 20);
    void Remove<T>(ICollection<T> value);
}