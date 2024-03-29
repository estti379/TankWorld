﻿using System;
using System.Collections.Generic;
using TankWorld.Engine;
using TankWorld.Game.Commands;
using TankWorld.Game.Events;
using TankWorld.Game.Items;
using static TankWorld.Engine.InputEnum;
using static TankWorld.Game.Panels.MapTypeEnum;

namespace TankWorld.Game.Panels
{

    public class PlayScene: Scene, IObserver
    {
        private List<Event> events;

        private WorldItems world;

        private PlayParameters playParameters;

        private MapPanel map;
        private GameViewPanel gameView;
        private UiPanel uiView;
        private MenuPanel menu;

        private Camera camera;

        private Timer spawnTimer;

        private int enemyDamaged;
        private int playerDamaged;

        bool showMenu;
        bool showHitboxes;

        //Constructors
        public PlayScene(PlayParameters parameters)
        {
            playParameters = parameters;
        }


        //Accessors
        public ref WorldItems World { get => ref world; }
        public Camera CurrentCamera { get => camera;}


        //Methods
        public override void Enter()
        {
            //create camera
            camera = Camera.Instance;
            camera.SetSubScreenDimensions(0, 0, WindowX, WindowY);
            //create map Panel
            switch (playParameters.mapType)
            {
                case UNDEFINED:
                    Console.WriteLine("Trying to create an UNDEFINED map !");
                    break;
                case TILED:
                    map = new TiledMapPanel(this, playParameters);
                    break;
                case UNLIMITED:
                    map = new UnlimitedMapPanel(this);
                    break;
            }
            //Create mainGamePanel
            gameView = new GameViewPanel(this);

            //Create Menu Items, add MenuPanel and initialize it
            List<MenuItem> menuItems = new List<MenuItem>();
            menuItems.Add(new MenuItem(new FlipMenuCommand(), "Continue", "Continue Game"));
            menuItems.Add(new MenuItem(new StartGameCommand(playParameters), "Restart", "Restart Level"));
            menuItems.Add(new MenuItem(new BackToMenuCommand(), "Back", "Back To Main Menu"));
            menuItems.Add(new MenuItem(new QuitGameCommand(), "Quit", "Quit Game"));
            menu = new MenuPanel(menuItems);
            menu.SetPosition((WindowX * 1 / 3), 100);
            showMenu = false;
            showHitboxes = false;

            uiView = new UiPanel(WindowX, WindowY);
            MainEventBus.Register(this);
            events = new List<Event>();

            spawnTimer = new Timer(Timer.Type.ASCENDING);
            spawnTimer.Time = 5 * 1000;
            spawnTimer.ExecuteTime = 5 * 1000;
            spawnTimer.DefaultTime = 0;
            spawnTimer.Command = new SpawnCommand(spawnTimer);

        }

        public override void Exit()
        {
            Sprite.RemoveAll();
        }

        public override void HandleInput(InputStruct input)
        {

            if(input.inputEvent == PRESS_ESCAPE)
            {
                showMenu = !showMenu;
            }
            if (showMenu)
            {
                switch (input.inputEvent)
                {
                    case PRESS_S:
                        menu.GoDown();
                        break;
                    case PRESS_DOWN:
                        menu.GoDown();
                        break;
                    case PRESS_W:
                        menu.GoUp();
                        break;
                    case PRESS_UP:
                        menu.GoUp();
                        break;
                    case PRESS_SPACE:
                        menu.Act();
                        break;
                    case PRESS_ENTER:
                        menu.Act();
                        break;
                    case PRESS_O:
                        showHitboxes = !showHitboxes;
                        break;
                } 
            }
            else
            {
                //switch (input.inputEvent)
                //{
                //    case PRESS_A:
                //        GameContext.Instance.ChangeResolution(1280, 720);
                //        Camera.Instance.SetSubScreenDimensions(0, 0, 1280, 720);
                //        break;
                //    case PRESS_D:
                //        GameContext.Instance.ChangeResolution(1920, 1080);
                //        Camera.Instance.SetSubScreenDimensions(0, 0, 1920, 1080);
                //        break;
                //    case PRESS_P:
                //        GameContext.Instance.ToggleFullScreen();
                //        break;
                //}
                gameView.HandleInput(input);
            }
            

        }

        public void OnEvent(Event newEvent)
        {
            events.Add(newEvent);
        }

        public override void Render(RenderLayer layer)
        {
            if (layer == RenderLayer.BACKGROOUND || layer == RenderLayer.GROUND || layer == RenderLayer.HITBOXES || layer == RenderLayer.MAINBOARD || layer == RenderLayer.OVERHEAD)
            {
                //don't render if layer is hitBoxes and showHitboxes is false!
                if ( !(layer == RenderLayer.HITBOXES && !showHitboxes) )
                {
                    map.Render(layer);
                    gameView.Render(layer);
                }
            }
            else if (showMenu && layer == RenderLayer.MENU)
            {
                menu.Render(layer);
            }
            else if (!showMenu && layer == RenderLayer.USER_INTERFACE)
            {
                //TODO:Bug:Clock stops being shown when menu is up
                uiView.Render(layer);
            }

        }

        public override void Update()
        {
            //carefull, when creating new objects while game is paused. they will come into existence out of thin air!
            PollEvents();
            if (showMenu)
            {
                menu.Update();
            }
            else
            {
                gameView.Update();
                camera.Update();
                uiView.Update();
                map.Update();
                spawnTimer.Update();
            }
        }

        private void PollEvents()
        {
            List<Event> events = new List<Event>();
            events.AddRange(this.events);
            this.events.Clear();

            SceneStateEvent stateEvent;
            foreach (Event entry in events)
            {
                if ((stateEvent = entry as SceneStateEvent) != null)
                {
                    switch (stateEvent.eventType)
                    {
                        case SceneStateEvent.Type.FLIP_MENU:
                            showMenu = !showMenu;
                            break;
                        case SceneStateEvent.Type.SPAWN_NEW_ENTITY:
                            gameView.AddNewObject(stateEvent.Sender);
                            break;
                        case SceneStateEvent.Type.DESPAWN_ENTITY:
                            gameView.RemoveObject(stateEvent.Sender);
                            break;
                        case SceneStateEvent.Type.TANK_HIT:
                            if(stateEvent.Target.Id == world.player.Id)
                            {
                                this.playerDamaged++;
                            }
                            else if(stateEvent.Sniper.Id == world.player.Id)
                            {
                                this.enemyDamaged++;
                            }
                            break;
                        case SceneStateEvent.Type.SPAWN_GROUP:
                            Coordinate spawnPosition;
                            spawnPosition = world.player.Position;
                            double angle = Helper.random.NextDouble();
                            spawnPosition.x += 1200 * Math.Cos(2 * Math.PI * angle);
                            spawnPosition.y += 1200 * Math.Sin(2 * Math.PI * angle);
                            TankObject newTank;
                            for (int i = 0; i < 3; i++)
                            {
                                angle = Helper.random.NextDouble();
                                spawnPosition.x += 200 * Math.Cos(2 * Math.PI * angle );
                                spawnPosition.y += 200 * Math.Sin(2 * Math.PI * angle );
                                newTank = new TankObject(spawnPosition, TankObject.TankColor.GREEN);
                                gameView.AddNewObject(newTank);
                            }
                            break;
                        case SceneStateEvent.Type.TIME_UP:
                            uiView.TimeIsUp(enemyDamaged, playerDamaged);
                            break;

                    }
                }
            }
        }
    }
}
