﻿using System;
using System.Collections.Generic;
using TankWorld.Engine;
using TankWorld.Game.Commands;
using TankWorld.Game.Components;
using TankWorld.Game.Events;
using TankWorld.Game.Models;

namespace TankWorld.Game.Items
{
    public class TankObject: GameObject , ICollide
    {

        public enum TankColor
        {
            PLAYER,
            GREEN
        }
        public enum Faction
        {
            PLAYER,
            AI
        }

        private AiComponent aiComponent;
        private PhysicsComponent tankPhysics;

        private TankModel model;
        private TankColor color;
        private Faction currentFaction;

        private Camera camera;
        //timers in milliseconds
        private Timer BulletSalvoTimer;
        private Timer CannonCooldownTimer;
        private Timer hpTimer;

        private double x = 4;
        
        //Top speed expressed in m/s
        private const double TOP_SPEED = 10;
        private const double TOP_SPEED_REVERSE = -TOP_SPEED/2;
        //degrees turned per second at MaxRate
        private const double MAX_DEGREE_PER_SECONDS_TURN = 180;
        //Max Acceleration at Maxrate expressed in m/s^2
        private const double MAX_ACCELERATION = 25;

        private const double SECONDS_TO_STOP = 2;

        private const double deepWaterSpeedMod = 0.1;
        private const double deepWaterAccelMod = 0.3;

        private const double waterSpeedMod = 0.3;
        private const double waterAccelMod = 0.3;

        private const double roadSpeedMod = 1.4;
        private const double roadAccelMod = 1.2;

        private const double dirtSpeedMod = 0.6;
        private const double dirtAccelMod = 0.9;

        private const double grassSpeedMod = 0.9;
        private const double grassAccelMod = 0.6;

        private const double sandSpeedMod = 0.5;
        private const double sandAccelMod = 0.3;


        //Weapon constants
        /*TODO: looks like weapons could become it's own instance of a class "Weapon"
         *which would allow for weapon modularity!
         */
        private const int CANNON_SALVO_PROJECTILE_NUMBER = 4;
        private const int CANNON_COOLDOWN = 1000; //Time in milliseconds before cannon can shoot new salvo
        //A thirst percentage of cannon cooldown time is used to shoot salvo. Each bullet is spread out evenly
        //Keep in mind that one bullet is shot instantly (thus the -1)
        private const double CANNON_BULLET_COOLDOWN = (10.0/100) * (double)CANNON_COOLDOWN / (CANNON_SALVO_PROJECTILE_NUMBER-1);
        //Prototype used to send to projectileSpawners
        //This prototype holds the state of a bullet that would be shot by the tank at any given moment
        private BulletObject cannonBulletPrototype;
        private WeaponProjectileSpawner cannonBulletSpawner;

        
        private double speed;
        private double acceleration;
        private double turningAngle;
        private double directionBody;
        private double directionCannon;
        private Coordinate cannonTarget;

        private double forwardRate;
        private double reverseRate;
        private double turnLeftRate;
        private double turnRightRate;

        private int maxHP;
        private int currentHP;



        //Constructors
        public TankObject(Coordinate spawnPosition, TankColor type):base()
        {
            camera = Camera.Instance;
            this.color = type;
            model = new TankModel(this.color);
            Position = spawnPosition;
            maxHP = 100;
            currentHP = maxHP;
            speed = 0;
            acceleration = 0;
            turningAngle = 0;
            directionBody = 3*Math.PI/2;
            cannonTarget = Position; //So that cannon is facing forward
            cannonTarget.x = (model.AllSprites["TankBody"].SubRect.w) * Math.Cos(directionBody) + Position.x;
            cannonTarget.y = (model.AllSprites["TankBody"].SubRect.w) * Math.Sin(directionBody) + Position.y;
            UpdateCannonDirection();
            model.UpdateModel(this, directionBody, directionCannon);
            cannonBulletPrototype = new BulletObject(this, this.GetBarrelEndPosition(), this.DirectionCannon);
            cannonBulletSpawner = new WeaponProjectileSpawner(cannonBulletPrototype);
            //initialize Timers only after setting spawners and their prototypes!
            InitializeTimers();

            tankPhysics = new TankPhysicsComponent(this);

            if (color == TankColor.PLAYER)
            {
                aiComponent = new DefaultAiComponent();
                currentFaction = Faction.PLAYER;
            }
            else
            {
                
                aiComponent = new TankAiComponent();
                //Use only for debugging, it neuters Tanks AI
                //aiComponent = new DefaultAiComponent();
                currentFaction = Faction.AI;
            }

        }

        private void InitializeTimers()
        {
            BulletSalvoTimer = new Timer(Timer.Type.DESCENDING)
            {
                DefaultTime = CANNON_BULLET_COOLDOWN * (CANNON_SALVO_PROJECTILE_NUMBER - 1),
                ExecuteTime = 0                
            };
            BulletSalvoTimer.Command = new SalvoShotCommand(this, BulletSalvoTimer, cannonBulletSpawner, CANNON_BULLET_COOLDOWN);
            BulletSalvoTimer.Pause();

            CannonCooldownTimer = new Timer(Timer.Type.PAUSE_AT_ZERO)
            {
                Time = 0,
                DefaultTime = CANNON_COOLDOWN,
                ExecuteTime = CANNON_COOLDOWN * 2//avoid execution
        };
            CannonCooldownTimer.Pause();

            hpTimer = new Timer(Timer.Type.PAUSE_AT_ZERO)
            {
                Time = 0,
                DefaultTime = 2000,
                ExecuteTime = 0
            };
            hpTimer.Command = new HideTankHPCommand(this, hpTimer);
            CannonCooldownTimer.Pause();
        }

        //Accessors

        public Faction CurrentFaction { get => currentFaction;}
        public Coordinate CannonTarget { get => cannonTarget; set => cannonTarget = value; }
        public double DirectionCannon { get => directionCannon;}
        public double DirectionBody { get => directionBody; }
        public double ForwardRate { get => forwardRate;}
        public double ReverseRate { get => reverseRate;}
        public double TurnLeftRate { get => turnLeftRate;}
        public double TurnRightRate { get => turnRightRate;}
        public TankModel Model { get => model;}
        public Coordinate SpeedVektor
        {
            get
            {
                Coordinate speedVektor;
                speedVektor.x = speed * Math.Cos(directionBody) * GameConstants.MS_PER_UPDATE * 1 / 1000 * GameConstants.METER_TO_PIXEL;
                speedVektor.y = speed * Math.Sin(directionBody) * GameConstants.MS_PER_UPDATE * 1 / 1000 * GameConstants.METER_TO_PIXEL;
                return speedVektor;
            }
        }

        public bool ShowHP { get => model.ShowHP; set => model.ShowHP = value; }
        public int MaxHP { get => maxHP;}
        public int CurrentHP { get => currentHP;}


        //Methods
        public double GetTopSpeed()
        {
            return TOP_SPEED;
        }
        public override void Render(RenderLayer layer)
        {
            if (camera.IsInsideCamera(this.Position, model.AllSprites["TankBody"].Pos.w, model.AllSprites["TankBody"].Pos.h) )
            {
                if (layer == RenderLayer.MAINBOARD)
                {
                    model.Render(layer);
                }
                else if (layer == RenderLayer.HITBOXES)
                {
                    tankPhysics.RenderHitBoxes();
                }
            }
            
        }

        public override void Update(ref WorldItems world)
        {
            aiComponent.Update(this, ref world);
            tankPhysics.Update(this, ref world);
            UpdateDirection();
            UpdateSpeed();
            UpdateCoordinates();
            UpdateCannonDirection();
            UpdateWeaponPrototypes();
            model.UpdateModel(this, directionBody, directionCannon);
            UpdateTimers();
            CheckLongivity();
        }

        private void CheckLongivity()
        {
            if (Helper.Distance(this.Position, camera.Position) > 10000)
            {
                MainEventBus.PostEvent(new SceneStateEvent(SceneStateEvent.Type.DESPAWN_ENTITY, this));
            }
        }

        private void UpdateWeaponPrototypes()
        {
            cannonBulletPrototype.UpdateState(this, this.GetBarrelEndPosition(), this.DirectionCannon);
        }

        private void UpdateTimers()
        {
            BulletSalvoTimer.Update();
            CannonCooldownTimer.Update();
            hpTimer.Update();
        }

        private void UpdateDirection()
        {
            directionBody += turningAngle *GameConstants.MS_PER_UPDATE*1/1000;
            directionBody = Helper.NormalizeRad(directionBody);
        }

        private void UpdateSpeed()
        {
            double oldSpeed = speed;
            double a =  TOP_SPEED/Math.Pow(SECONDS_TO_STOP, x);
            double t = 10 - (Math.Pow(Math.Abs(oldSpeed), 1.0/x) / Math.Pow(a, 1.0/x) );
            double speedDecay = -x*a*Math.Pow( 10 - t, 1.0/(x-1) );
            
            //Apply speed_decay on top of acceleration
            if (speed < 0)
            {
                speed -= speedDecay * GameConstants.MS_PER_UPDATE * 1 / 1000;
            } else if (speed > 0)
            {
                speed += speedDecay * GameConstants.MS_PER_UPDATE * 1 / 1000;
            }
            //If there is no acceleration, allow tank to completely stop
            if (acceleration == 0)
            {
                //If speed goes from negative to positive (or the other way around), set it to 0
                if ( (oldSpeed < 0 && speed >0) || (oldSpeed > 0 && speed < 0) )
                {
                    speed = 0;
                }
                
            }
            //Add Acceleration to currentSpeed. Avoid going over TOP_SPEED!


            double trueAccel = acceleration - Math.Pow(oldSpeed/TOP_SPEED, x)* acceleration ;


            speed += trueAccel * GameConstants.MS_PER_UPDATE * 1 / 1000 * GetAccelModifier();
            if (speed > TOP_SPEED * GetSpeedModifier())
            {
                speed = TOP_SPEED * GetSpeedModifier();
            }
            else if(speed < TOP_SPEED_REVERSE * GetSpeedModifier())
            {
                speed = TOP_SPEED_REVERSE * GetSpeedModifier();
            }
            

        }

        private double GetSpeedModifier()
        {


            return 1;
        }

        private double GetAccelModifier()
        {

            return 1;
        }

        private void UpdateCoordinates()
        {
            Position.x += speed * Math.Cos(directionBody) * GameConstants.MS_PER_UPDATE * 1 / 1000 * GameConstants.METER_TO_PIXEL;
            Position.y += speed * Math.Sin(directionBody) * GameConstants.MS_PER_UPDATE * 1 / 1000 * GameConstants.METER_TO_PIXEL;
        }

        public void UpdateCannonDirection()
        {
            Coordinate turretCoord = GetTurretPosition();

            /* TODO: a player tank has a different behaviour than an not player tank.
             * Create a new Class wich extends tankobject and override this method?
             */
            if(color == TankColor.PLAYER) //Player cannon should follow current mouse position on the screen
            {
                cannonTarget.x += camera.Position.x - camera.OldPosition.x;
                cannonTarget.y += camera.Position.y - camera.OldPosition.y;
            }
            else //All Other tanks shouldn't change their target location, since they are targeting a mapLocation, not a screen location, like the player
            {
                /*empty*/
            }
            

            //If mouse is at the same pixel as the turret center, don't calculate angle.
            if ( !( (cannonTarget.y - turretCoord.y == 0) && (cannonTarget.x - turretCoord.x == 0) ) )
            {
                directionCannon = Math.Atan2(cannonTarget.y - turretCoord.y, cannonTarget.x - turretCoord.x);
            }

            directionCannon = Helper.NormalizeRad(directionCannon);


        }

        public Coordinate GetTurretPosition()
        {
            Coordinate turretCoord;
            turretCoord.x = (model.AllSprites["TankBody"].SubRect.w / 4) * Math.Cos(directionBody + Math.PI) + Position.x;
            turretCoord.y = (model.AllSprites["TankBody"].SubRect.w / 4) * Math.Sin(directionBody + Math.PI) + Position.y;

            return turretCoord;
        }

        public Coordinate GetCannonPosition()
        {
            Coordinate turretCoord = GetTurretPosition();
            Coordinate cannonCoord;

            cannonCoord.x = (model.AllSprites["TankCannon"].SubRect.w / 2) * Math.Cos(directionCannon) + turretCoord.x;
            cannonCoord.y = (model.AllSprites["TankCannon"].SubRect.w / 2) * Math.Sin(directionCannon) + turretCoord.y;

            return cannonCoord;
        }

        public Coordinate GetBarrelEndPosition()
        {
            Coordinate turretCoord = GetTurretPosition();
            Coordinate barrelCoord;

            barrelCoord.x = (model.AllSprites["TankTurret"].SubRect.w) * Math.Cos(directionCannon) + turretCoord.x;
            barrelCoord.y = (model.AllSprites["TankTurret"].SubRect.w) * Math.Sin(directionCannon) + turretCoord.y;

            return barrelCoord;
        }


        private void Accelerate()
        {
            this.acceleration = (reverseRate+forwardRate) * MAX_ACCELERATION;
        }
        private void Turn()
        {
            turningAngle = (turnLeftRate+turnRightRate) * MAX_DEGREE_PER_SECONDS_TURN * Math.PI/180;
        }

        public void Forward(double rate)
        {
            forwardRate = rate;
            Accelerate();
        }
        public void Reverse(double rate)
        {
            reverseRate = -rate;
            Accelerate();
        }
        public void TurnLeft(double rate)
        {
            turnLeftRate = -rate;
            Turn();
        }
        public void TurnRight(double rate)
        {
            turnRightRate = rate;
            Turn();
        }

        public void TurretTarget(int x, int y)
        {
            cannonTarget.x = x;
            cannonTarget.y = y;
            cannonTarget = camera.ConvertScreenToMapCoordinate(cannonTarget);


        }
        public void Shoot()
        {
            if (CannonCooldownTimer.Time <= 0)
            {
                CannonCooldownTimer.Reset();
                CannonCooldownTimer.UnPause();
                BulletSalvoTimer.Reset();
                BulletSalvoTimer.UnPause();
                BulletSalvoTimer.ExecuteTime = BulletSalvoTimer.DefaultTime;
            }
        }

        public HitBoxStruct GetHitBoxes()
        {
            return this.tankPhysics.HitBoxes;
        }

        public void CheckForCollision(ICollide collidingObject)
        {
            Coordinate collisionPoint = new Coordinate();
            foreach (KeyValuePair<string, HitBox> myBox in this.GetHitBoxes().hitBoxesList)
            {
                foreach (KeyValuePair<string, HitBox> otherBox in collidingObject.GetHitBoxes().hitBoxesList)
                {
                    if (Helper.HitBoxIntersection(myBox.Value, otherBox.Value, ref collisionPoint))
                    {
                        this.HandleCollision(collidingObject, collisionPoint);
                        break; //Stop right after first point found!
                        //TODO:Bug: One does leave one foreach with this break, but first foreach keeps going!
                    }
                }
            }
        }

        public void HandleCollision(ICollide collidingObject, Coordinate collisionPoint)
        {
            /*Do nothing yet*/
        }

        internal void TakeDamage(int damage)
        {
            currentHP -= damage;
            ShowHP = true;
            hpTimer.Reset();
            hpTimer.UnPause();
            if(currentHP <= 0)
            {
                if(this.currentFaction == Faction.PLAYER)
                {
                    Console.WriteLine("Player died!");
                    currentHP = maxHP;
                }
                else
                {
                    MainEventBus.PostEvent(new SceneStateEvent(SceneStateEvent.Type.DESPAWN_ENTITY, this));
                }
            }
        }
    }
}