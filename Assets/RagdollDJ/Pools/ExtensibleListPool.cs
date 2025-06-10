using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// List item
/// </summary>
/// <typeparam name="T"></typeparam>
public class ExtensibleListPool<T> : IContainerPoolBase where T : new()
{
    /// <summary>
    /// Get size
    /// </summary>
    private int m_iSize = 0;

    /// <summary>
    /// Data reference
    /// </summary>
    private List<T> m_pData = null;
    public List<T> Data
    {
        get { return m_pData; }
    }

    /// <summary>
    /// Constructed Function
    /// </summary>
    public ExtensibleListPool(int defaultSize)
    {
        Init(defaultSize);
    }

    /// <summary>
    /// Init with default size
    /// </summary>
    /// <param name="defaultSize"></param>
    public void Init(int defaultSize)
    {
        m_pData = new List<T>(defaultSize);
        for (int i = 0; i < defaultSize; i++)
        {
            m_pData.Add(new T());
        }
        m_iSize = defaultSize;
    }

    /// <summary>
    /// Get one
    /// </summary>
    /// <returns></returns>
    public bool GetItem(int index,out T t)
    {
        if(index < m_iSize)
        {
            t = m_pData[index];
            return true;
        }
        else if(index == m_iSize)
        {
            t = new T();
            m_pData.Add(t);
            m_iSize++;
            return true;
        }
        throw new System.Exception("Index error!" + index.ToString());
    }

    /// <summary>
    /// Clear
    /// </summary>
    public void Clear()
    {
        if (null != m_pData)
            m_pData.Clear();
        m_pData = null;
        m_iSize = 0;
    }

    /// <summary>
    /// Trim
    /// </summary>
    public void Trim()
    {
    }

    /// <summary>
    /// Size
    /// </summary>
    /// <returns></returns>
    public int Size()
    {
        return m_iSize;
    }
}

