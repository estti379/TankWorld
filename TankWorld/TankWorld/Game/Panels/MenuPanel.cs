﻿using System.Collections.Generic;
using TankWorld.Engine;

namespace TankWorld.Game.Panels
{
    class MenuPanel: Panel
    {
        List<MenuItem> items;
        int activeItemIndex;
        
        //Constructors
        public MenuPanel(List<MenuItem> items)
        {
            this.items = items;
            activeItemIndex = 0;
            UpdateCurrentItem();
        }

        //Accessors


        //Methods

        public override void Render(RenderLayer layer)
        {
            foreach(MenuItem entry in items)
            {
                entry.Render(layer);
            }
        }

        public override void Update()
        {
            foreach (MenuItem entry in items)
            {
                entry.Update();
            }
        }

        public void SetPosition(int x, int y)
        {
            for(int i = 0; i < items.Count ; i++)
            {
                items[i].SetPosition(x,y,i);
            }
        }

        public void GoDown()
        {
            UpdateCurrentItem();
            activeItemIndex = (activeItemIndex + 1) % items.Count;
            UpdateCurrentItem();
        }

        public void GoUp()
        {
            UpdateCurrentItem();
            activeItemIndex = (activeItemIndex - 1 + items.Count) % items.Count;
            UpdateCurrentItem();
        }
        public void Act()
        {
            items[activeItemIndex].Action();
        }

        private void UpdateCurrentItem()
        {
            items[activeItemIndex].ChangeStatus();
        }
    }
}
