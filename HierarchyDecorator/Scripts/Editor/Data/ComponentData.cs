using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HierarchyDecorator
{

    //TODO: [Wooshii] Add XML Comments to new API
    [Serializable]
    public class ComponentData
    {
        // --- Editor Settings

        private readonly static CategoryFilter DefaultFilter = new CategoryFilter("General", string.Empty, FilterType.NONE);

        public readonly static CategoryFilter[] ComponentFilters =
        {
            new CategoryFilter ("2D", "2D", FilterType.NAME),

            // Animation
            new CategoryFilter ("Animation", "Anim", FilterType.NAME),
            new CategoryFilter ("Animation", "Constraint", FilterType.NAME),

            // Audio
            new CategoryFilter ("Audio", "Audio", FilterType.NAME),

            // Mesh
            new CategoryFilter ("Mesh", "Renderer", FilterType.NAME),
            new CategoryFilter ("Mesh", "Mesh", FilterType.NAME),

            // Physics
            new CategoryFilter ("Physics", "Collider", FilterType.NAME),
            new CategoryFilter ("Physics", "Joint", FilterType.NAME),
            new CategoryFilter ("Physics", "Rigidbody", FilterType.NAME),

            // Networking
            new CategoryFilter ("Network", "Network", FilterType.NAME),

            new CategoryFilter ("UI", "Canvas", FilterType.NAME),
            new CategoryFilter ("UI", "UnityEngine.EventSystems.UIBehaviour, UnityEngine.UI", FilterType.TYPE),
            new CategoryFilter ("UI", "UnityEngine.GUIElement, UnityEngine", FilterType.TYPE)
        };

        // --- Settings

        [SerializeField] private bool showMissingScriptWarning;

        [SerializeField] private ComponentGroup[] unityGroups = new ComponentGroup[0];

        [SerializeField] private List<ComponentGroup> customGroups = new List<ComponentGroup>();
        [SerializeField] private ComponentGroup allCustomComponents = new ComponentGroup("All");

        // --- Validition

        [SerializeField] private string unityVersion;
        [SerializeField] private bool isDirty;

        // --- Reflection

        private Type[] allTypes;

        // --- Properties

        /// <summary>
        /// The number of Unity Components that have been found.
        /// </summary>
        public int UnityCount;

        /// <summary>
        /// Is the missing script warning on?
        /// </summary>
        public bool ShowMissingScriptWarning
        {
            get
            {
                return showMissingScriptWarning;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public ComponentGroup[] UnityGroups
        {
            get
            {
                return unityGroups;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public ComponentGroup[] CustomGroups
        {
            get
            {
                return customGroups.ToArray();
            }
        }

        public ComponentGroup AllCustomComponents
        {
            get
            {
                return allCustomComponents;
            }
        }

        // --- 

        /// <summary>
        /// 
        /// </summary>
        public void OnInitialize()
        {
            isDirty = unityVersion != Application.unityVersion;

            if (isDirty)
            {
                unityVersion = Application.unityVersion;
                Debug.LogWarning ("HierarchyDecorator version changed, updating cached components.");
            }

            UpdateData();
        }

        /// <summary>
        /// Update Component Data
        /// </summary>
        public void UpdateData()
        {

            // Generate all types found in the Unity project

            if (allTypes == null || isDirty)
            {
                allTypes = ReflectionUtility.GetSubTypesFromAssemblies(typeof(Component), t => t.Assembly.FullName.Contains("Unity"))
                    .OrderBy(t => t.Name)
                    .ToArray();
            }

            // Update the components if any are missing

            if (!isDirty)
            {
                isDirty = UnityCount != allTypes.Length || unityGroups.Length == 0;
            }

            if (isDirty)
            {
                // Get the count of unity types 

                UnityCount = allTypes.Length;
                Dictionary<string, ComponentGroup> cachedGroups = new Dictionary<string, ComponentGroup>();

                for (int i = 0; i < UnityCount; i++)
                {
                    // Find the category for the type 
                    // If the group doesn't exist, create one

                    Type type = allTypes[i];
                    string category = GetTypeCategory(type);

                    // 

                    if (!TryGetComponent(type, out ComponentType component))
                    {
                        component = new ComponentType(type, true);
                    }
                    
                    // 

                    if (!cachedGroups.TryGetValue(category, out ComponentGroup group))
                    {
                        group = new ComponentGroup(category);
                        cachedGroups.Add(category, group);
                    }

                    group.Add(component);
                }

                // 

                unityGroups = cachedGroups.Values.ToArray();
                isDirty = false;
            }
        }
        
        /// <summary>
        /// Update the components type and content.
        /// </summary>
        /// <param name="updateContent">Should the gui content be updated too?</param>
        public void UpdateComponents(bool updateContent = true)
        {
            // Update built in components first
            
            for (int i = 0; i < unityGroups.Length; i++)
            {
                ComponentGroup group = unityGroups[i]; 

                for (int j = 0; j < group.Count; j++)
                {
                    ComponentType component = group.Get(j);

                    // Take into account any deleted components, remove and deincrement the loop

                    if (component == null)
                    {
                        group.Remove(component);
                        j--;

                        continue;
                    }

                    // Do not update if the component already has a type

                    if (component.Type != null)
                    {
                        continue;
                    }

                    // Find the type the component requires and assign it
                    // Update the content if required too
                    
                    int index = Array.FindIndex(allTypes, type => type.Name == component.Name);
                    component.UpdateType(allTypes[index], updateContent);
                }
            }

            // Update custom components 

            for (int i = 0; i < allCustomComponents.Count; i++)
            {
                ComponentType component = allCustomComponents.Get(i);
                component.UpdateType(component.Script.GetClass(), updateContent);
            }

            for (int i = 0; i < customGroups.Count; i++)
            {
                ComponentGroup group = customGroups[i];

                for (int j = 0; j < group.Count; j++)
                {
                    ComponentType component = group.Get(j);

                    // Do not update if the component already has a type

                    if (component.Type != null || component.Script == null)
                    {
                        continue;
                    }

                    // Update the type and the content (if required)

                    component.UpdateType(component.Script.GetClass(), updateContent);
                }
            }
        }
        
        /// <summary>
        /// Set component data to dirty. 
        /// </summary>
        public void SetDirty()
        {
            isDirty = true;
        }

        // --- Custom Components

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        public void AddCustomGroup(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogError("Attempted to add a custom group with no name.");
                return;
            }

            customGroups.Add(new ComponentGroup(name));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        public void DeleteCustomGroup(int index)
        {
            if (index < 0 || index >= customGroups.Count)
            {
                Debug.LogError($"Index out of range of customGroup collection size.");
                return;
            }

            customGroups.RemoveAt(index);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="component"></param>
        public void RegisterCustomComponent(ComponentType component)
        {
            Debug.Log(component);

            if (component == null)
            {

                return;
            }

            if (allCustomComponents.Contains(component))
            {

                return;
            }

            allCustomComponents.Add(component);
        }

        // --- Queries

        /// <summary>
        /// Find a component with the given type.
        /// </summary>
        /// <param name="type">The type to find</param>
        /// <param name="component">The component returned if one is found. Otherwise will be null.</param>
        /// <returns>Returns true if a component was found, otherwise will return false.</returns>
        public bool TryGetComponent(Type type, out ComponentType component)
        {
            for (int i = 0; i < unityGroups.Length; i++)
            {
                ComponentGroup group = unityGroups[i];

                for (int j = 0; j < group.Count; j++)
                {
                    component = group.Get(j);
                    
                    //

                    if (component.Type == type)
                    {
                        return true;
                    }
                }
            }

            // 

            component = null;
            return false;
        }

        /// <summary>
        /// Find a custom component with the given type.
        /// </summary>
        /// <param name="type">The type to find</param>
        /// <param name="component">The component returned if one is found. Otherwise this will be null.</param>
        /// <returns>Returns true if a component was found, otherwise will return false.</returns>
        public bool TryGetCustomComponent(Type type, out ComponentType component)
        {
            if (type == null)
            {
                component = null;
                return false;
            }

            for (int i = 0; i < allCustomComponents.Count; i++)
            {
                component = allCustomComponents.Get(i);

                if (component.Script == null)
                {
                    continue;
                }

                if (component.Type == type)
                {
                    return true;
                }
            }

            component = null;
            return false;
        }

        /// <summary>
        /// Get the category the given type belongs to.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>Returns the category for the type provided.</returns>
        private string GetTypeCategory(Type type)
        {
            //
            
            if (type == null)
            {
                return null;
            }

            // 

            foreach (var filter in ComponentFilters)
            {
                //
                
                if (filter.IsValidFilter(type))
                {
                    return filter.Name;
                }
            }

            // 
           
            return DefaultFilter.Name;
        }
    }
}