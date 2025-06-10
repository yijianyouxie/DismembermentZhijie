using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

/// <summary>
/// List item
/// </summary>
/// <typeparam name="T"></typeparam>
public class ListItem<T> : IContainerPoolItem<T>
{
    /// <summary>
    /// Is Using
    /// </summary>
    private bool m_bIsUsing = false;

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
    public ListItem(int defaultSize)
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
    }

    /// <summary>
    /// Try Get item
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool Get(out ICollection<T> value)
    {
        if(m_bIsUsing)
        {
            value = null;
            return false;
        }
        m_bIsUsing = true;
        value = m_pData;
        return true;
    }

    /// <summary>
    /// THis item is using
    /// </summary>
    /// <returns></returns>
    public bool IsUsing()
    {
        return m_bIsUsing;
    }

    /// <summary>
    /// Clear
    /// </summary>
    public void Clear()
    {
        if (null != m_pData)
            m_pData.Clear();
        m_pData = null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public void Remove()
    {
        m_bIsUsing = false;
        if(null != m_pData)
        {
            m_pData.Clear();
        }  
    }

    /// <summary>
    /// Trim
    /// </summary>
    public void Trim()
    {
        if (null != m_pData)
            m_pData.TrimExcess();
    }

    /// <summary>
    /// Size
    /// </summary>
    /// <returns></returns>
    public int Size()
    {
        return null == m_pData ? 0 : m_pData.Count;
    }
}


/// <summary>
/// List pool
/// </summary>
public class ListPool : IContainerPool
{
    /// <summary>
    /// Default size
    /// </summary>
    private const int DefaultSize = 20;

    /// <summary>
    /// Max item count
    /// </summary>
    private const int kMaxItemCount = 50;

    /// <summary>
    /// Singleton
    /// </summary>
    public static readonly ListPool Instance = null;
    static ListPool()
    {
        Instance = new ListPool();
        Instance.Init(DefaultSize);
    }

    /// <summary>
    /// Keep reference for all pool items
    /// </summary>
    private List<object> m_listPoolItems = null;

    /// <summary>
    /// Constructed Function
    /// </summary>
    /// <param name="defaultSize"></param>
    private ListPool()
    {
    }

    /// <summary>
    /// Init
    /// </summary>
    /// <param name="defaultSize"></param>
    public void Init(int defaultSize)
    {
        m_listPoolItems = new List<object>(defaultSize);
    }

    /// <summary>
    /// Convert
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool Get<T>(out List<T> value, int defaultSize = 20)
    {
        if (Size() >= kMaxItemCount)
        {
            value = new List<T>(defaultSize);
            return true;
        }

        ICollection<T> retIt = null;
        bool canGet = Get<T>(out retIt,defaultSize);
        value = retIt as List<T>;
        return canGet;
    }

    /// <summary>
    /// Get
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool Get<T>(out ICollection<T> value,int defaultSize = 20)
    {
        if(null == m_listPoolItems)
        {
            value = null;
            return false;
        }

        for (int i = 0; i < m_listPoolItems.Count; ++i)
        {
            ListItem<T> it = m_listPoolItems[i] as ListItem<T>;
            if (null != it && !it.IsUsing())
            {
                return it.Get(out value);
            }
        }

        ListItem<T> nIt = new ListItem<T>(defaultSize);
        m_listPoolItems.Add(nIt);
        return nIt.Get(out value);
    }

    /// <summary>
    /// Remove
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    public void Remove<T>(ICollection<T> value)
    {
        if (Size() >= kMaxItemCount)
        {
            if (null != value)
                value.Clear();
            return;
        }

        if (null == m_listPoolItems || null == value)
        {
            return;
        }

        for (int i = 0; i < m_listPoolItems.Count; ++i)
        {
            ListItem<T> it = m_listPoolItems[i] as ListItem<T>;
            if (null != it && it.IsUsing() && it.Data == value)
            {
                it.Remove();
                break;
            }
        }
    }

    /// <summary>
    /// Size
    /// </summary>
    /// <returns></returns>
    public int Size()
    {
        return null == m_listPoolItems ? 0 : m_listPoolItems.Count;
    }

    /// <summary>
    /// Trim
    /// </summary>
    public void Trim()
    {
        if (null != m_listPoolItems)
        {
            m_listPoolItems.TrimExcess();
            for (int i = 0; i < m_listPoolItems.Count; ++i)
            {
                Type typeInfo = m_listPoolItems[i].GetType();
                if (null != typeInfo)
                {
                    MethodInfo method = typeInfo.GetMethod("Trim", BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
                    if (null != method)
                    {
                        method.Invoke(m_listPoolItems[i], null);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Clear
    /// </summary>
    public void Clear()
    {
        if (null != m_listPoolItems)
        {
            for (int i = 0; i < m_listPoolItems.Count; ++i)
            {
                Type typeInfo = m_listPoolItems[i].GetType();
                if (null != typeInfo)
                {
                    MethodInfo method = typeInfo.GetMethod("Clear", BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
                    if (null != method)
                    {
                        method.Invoke(m_listPoolItems[i], null);
                    }
                }
            }
            m_listPoolItems.Clear();
        }
    }
}

