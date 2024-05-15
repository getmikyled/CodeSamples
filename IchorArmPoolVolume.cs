using MichaelWolfGames;
using System.Collections.Generic;
using UnityEngine;
using WaxHeart.Hazards;


namespace WaxHeart
{
    //-//////////////////////////////////////////////////////////////
    ///
    /// When the player first enters the volume, the ichor pool
    /// enters the SpawningIchorState. In this state, ichor arms
    /// spawn surrounding the player after a timer ends.
    ///
    /// After, it goes into the BubblingIchor stage, where the ichor begins
    /// to bubble and intensify around the player.
    ///
    /// After the kill timer ends, PullUnderIchor state is entered. Ichor arms
    /// emerge from the surface at the feet of the player to pull
    /// them down.
    ///
    public enum IchorArmPoolState
    {
        Inactive = 0,
        
        SpawningIchorArms = 1,          
        BubblingIchor = 2,   
        PullingUnderIchor = 3,
        
        Deactivating = 4
    }
    
    //-//////////////////////////////////////////////////////////////
    ///
    [RequireComponent(typeof(StickyFloorVolume))]
    public class IchorArmPoolVolume : CompoundTriggerVolumeBase
    {
        private const float PREVENT_JUMP_CHEESING_THRESHOLD = 0.5f;

        [Header("Timers")]
        [Tooltip("The player will be killed by rising ichor arms animation after timer runs out.")]
        [SerializeField] private float _killTimerLength = 3.75f;
        private float _killTimeElapsed = 0f;
        [Tooltip("The initial ichor arms will spawn around the player after the timer runs out.")]
        [SerializeField] private float _spawnIchorArmsTimerLength = 1.5f;
        private float _spawnIchorArmsTimeElapsed = 0f;
        [SerializeField] private float _pullUnderEmergeDurationLength = 1f;
        private float _pullUnderEmergeTimeElapsed = 0f;
        private float timeSincePlayerExited = 0f;

        [Space]
        [Header("Ichor Arms")]
        [Tooltip("The array that contains the ichor arms that will spawn.")]
        [SerializeField] private IchorHazardArmController[] _ichorArms;             // Contains all the ichor arms in the pool
        [Tooltip("The number of ichor arms that will spawn.")]
        [SerializeField] private int _numIchorArmsToSpawn = 4;
        private int _ichorArmIndex = 0;
        [Tooltip("The amount of time it takes for the standard ichor arm to submerge upon grabbing the player.")]
        [SerializeField] private float _submergenceOnGrabDelayLength = 1f;
        [SerializeField] private GameObject _pullUnderIchorArms;
        [Tooltip("The radius around the player that defines the inner boundary that ichor arms can spawn in.")]
        [SerializeField] private float _innerSpawnRadius = 1f;
        [Tooltip("The radius around the player that defines the outer boundary that ichor arms can spawn in.")]
        [SerializeField] private float _outerSpawnRadius = 2f;

        [SerializeField] private Transform _ichorArmSpawnHeight;
        private float ichorArmSpawnHeight => _ichorArmSpawnHeight.position.y;

        [Space]
        [Header("VFX")]
        [SerializeField] private GameObject _pullUnderDistortionEffects;
        [SerializeField] private ParticleSystem _ichorBubblesVFX;
        [SerializeField] private float _ichorBubblesIncreaseRate = 1.2f;

        [Space]
        [Header("Animations")]
        [SerializeField] private Animator _pullUnderAnimator;
        private static int PullUnder_IsActivated_ParamID = Animator.StringToHash("isActivated");
        private static int PullUnderIchor_ParamID = Animator.StringToHash("Pull_Under_Ichor");
        private static int PullUnder_ForceSubmergeIchorArms_ParamID = Animator.StringToHash("Force_Submerge_Ichor_Arms");

        private IchorArmPoolState _currentState = IchorArmPoolState.Inactive;
        
        private Collider colliderContainingPlayer;
        
        private bool _isPullingPlayerUnder = false;
        private bool _arePullUnderIchorArmsEmerging = false;

        private bool playerJustEntered = false;
        private bool playerExited = false;

#region IchorArmPoolState methods

        //-///////////////////////////////////////////////////////////
        /// 
        private void SetPoolState(IchorArmPoolState argState)
        {
            if (argState == _currentState)
            {
                return;
            }
            IchorArmPoolState prevState = _currentState;
            _currentState = argState;
            
            switch (prevState)
            {
                case IchorArmPoolState.Inactive:
                    break;
                case IchorArmPoolState.SpawningIchorArms:
                    break;
                case IchorArmPoolState.BubblingIchor:
                    OnExitBubblingIchorState();
                    break;
                case IchorArmPoolState.PullingUnderIchor:
                    break;
                case IchorArmPoolState.Deactivating:
                    break;
            }
            
            switch (_currentState)
            {
                case IchorArmPoolState.Inactive:
                    break;
                case IchorArmPoolState.SpawningIchorArms:
                    OnEnterSpawningIchorArmsState();
                    break;
                case IchorArmPoolState.BubblingIchor:
                    OnEnterBubblingIchorState();
                    break;
                case IchorArmPoolState.PullingUnderIchor:
                    OnEnterPullingUnderIchorState();
                    break;
                case IchorArmPoolState.Deactivating:
                    OnEnterDeactivatingState();
                    break;
            }
        }
        
        //-///////////////////////////////////////////////////////////
        ///
        private void UpdateCurrentState()
        {
            switch (_currentState)
            {
                case IchorArmPoolState.Inactive:
                    break;
                case IchorArmPoolState.SpawningIchorArms:
                    OnUpdateSpawningIchorArmsState();
                    break;
                case IchorArmPoolState.BubblingIchor:
                    OnUpdateBubblingIchorState();
                    break;
                case IchorArmPoolState.PullingUnderIchor:
                    OnUpdatePullingUnderIchorState();
                    break;
                case IchorArmPoolState.Deactivating:
                    OnUpdateDeactivatingState();
                    break;
            }
        }
        
        //-///////////////////////////////////////////////////////////
        ///
        private void OnEnterSpawningIchorArmsState()
        {
            colliderContainingPlayer = GetColliderContainingPlayer();
            
            // Reset timers
            _spawnIchorArmsTimeElapsed = 0f;
        }
        
        //-///////////////////////////////////////////////////////////
        ///
        private void OnUpdateSpawningIchorArmsState()
        {
            if (_spawnIchorArmsTimeElapsed >= _spawnIchorArmsTimerLength)
            {
                SpawnIchorArms(_numIchorArmsToSpawn);
                SetPoolState(IchorArmPoolState.BubblingIchor);
            }

            _spawnIchorArmsTimeElapsed += Time.deltaTime;
        }

        //-///////////////////////////////////////////////////////////
        ///
        private void OnEnterBubblingIchorState()
        {
            // Activate ichor bubbles VFX
            Vector3 playerPosition = PlayerCharacterController.GetInstance().transform.position;
            _ichorBubblesVFX.transform.position = new Vector3(playerPosition.x, ichorArmSpawnHeight, playerPosition.z);
            _ichorBubblesVFX.gameObject.SetActive(true);
            ParticleSystem.EmissionModule ichorBubblesEmission = _ichorBubblesVFX.emission;
            ichorBubblesEmission.rateOverTimeMultiplier = 1f;
            
            // Reset Timer
            _killTimeElapsed = 0f;
        }
        
        //-///////////////////////////////////////////////////////////
        ///
        private void OnUpdateBubblingIchorState()
        {
            // Update Kill Timer
            if (_killTimeElapsed >= _killTimerLength)
            {
                SetPoolState(IchorArmPoolState.PullingUnderIchor);
            }

            _killTimeElapsed += Time.deltaTime;
            
            // Update IchorBubbles VFX
            Vector3 playerPosition = PlayerCharacterController.GetInstance().transform.position;
            
            // Update ichor bubbles vfx
            ParticleSystem.EmissionModule ichorBubblesEmission = _ichorBubblesVFX.emission;
            ichorBubblesEmission.rateOverTimeMultiplier += _ichorBubblesIncreaseRate * Time.deltaTime;
            _ichorBubblesVFX.transform.position = new Vector3(playerPosition.x, _ichorBubblesVFX.transform.position.y, playerPosition.z);
        }
        
        //-///////////////////////////////////////////////////////////
        ///
        private void OnExitBubblingIchorState()
        {
            _ichorBubblesVFX.gameObject.SetActive(false);
        }
        
        //-///////////////////////////////////////////////////////////
        ///
        private void OnEnterPullingUnderIchorState()
        {
            Vector3 playerPosition = PlayerCharacterController.GetInstance().transform.position;
            
            // Activate the smaller ichor arms that pull the player under
            _pullUnderIchorArms.SetActive(true);
            _pullUnderIchorArms.transform.position = new Vector3(playerPosition.x, ichorArmSpawnHeight, playerPosition.z);
            _pullUnderDistortionEffects.SetActive(true);

            // Play animations
            PlayerCharacterController.GetInstance().StartPulledUnderIchorAnimation();
            _pullUnderAnimator.SetBool(PullUnder_IsActivated_ParamID, true);
            _pullUnderAnimator.Play(PullUnderIchor_ParamID);
        }
        
        //-///////////////////////////////////////////////////////////
        ///
        private void OnUpdatePullingUnderIchorState()
        {
            Vector3 playerPosition = PlayerCharacterController.GetInstance().transform.position;
            
            // Update "pull under" distortion effects
            _pullUnderDistortionEffects.transform.position = new Vector3(playerPosition.x, _pullUnderDistortionEffects.transform.position.y, playerPosition.z);
        }
        
        //-///////////////////////////////////////////////////////////
        ///
        private void OnEnterDeactivatingState()
        {
            SubmergeAllIchorArms();
            
            // Deactivate ichor bubbles VFX
            _ichorBubblesVFX.gameObject.SetActive(false);
        }
        
        //-///////////////////////////////////////////////////////////
        ///
        private void OnUpdateDeactivatingState()
        {
            if (_ichorArms == null)
            {
                Debug.LogWarning("Ichor arms are not added to serialized list in IchorArmPoolVolume");
                return;
            }
            
            // Set pool state to inactive once ichor arms are submerged
            if (_ichorArms[0].currentState == IchorArmState.Idle)
            {
                SetPoolState(IchorArmPoolState.Inactive);
            }
        }

#endregion // IchorArmPoolState methods
        
        //-///////////////////////////////////////////////////////////
        /// 
        protected override void OnPlayerEnterVolume()
        {
            SetPoolState(IchorArmPoolState.SpawningIchorArms);

            if (timeSincePlayerExited > PREVENT_JUMP_CHEESING_THRESHOLD)
            {
                _killTimeElapsed = 0;
            }
            else 
            {
                /// Found multiplying by 1.7f makes jump cheesing as effective as normal walking. 
                _killTimeElapsed += timeSincePlayerExited * 1.7f;
            }

            playerJustEntered = true;
            playerExited = false;
        }

        //-///////////////////////////////////////////////////////////
        /// 
        protected override void OnPlayerExitVolume() 
        {
            if (PlayerCharacterController.GetInstance() == null)
            {
                return;
            }
            
            SetPoolState(IchorArmPoolState.Deactivating);
            
            timeSincePlayerExited = 0f;
            playerExited = true;
        }

        //-///////////////////////////////////////////////////////////
        /// 
        private void Update()
        {
            UpdateCurrentState();
        }

        //-///////////////////////////////////////////////////////////
        /// 
        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            if (playerExited && timeSincePlayerExited < PREVENT_JUMP_CHEESING_THRESHOLD)
            {
                timeSincePlayerExited += Time.fixedDeltaTime;
                return;
            }
        }

        //-///////////////////////////////////////////////////////////
        /// 
        private void SpawnIchorArms(int count = 1)
        {
            // Get spawn position and pivot position to rotate around
            // Pivot position is the player's predicted position after 0.25 seconds have passed.
            PlayerCharacterController player = PlayerCharacterController.GetInstance();
            Vector3 pivotPosition = player.transform.position + player.GetVelocity() * 0.25f;
            Vector3 positionToRotate = player.transform.forward * Random.Range(_innerSpawnRadius, _outerSpawnRadius);
            
            // Get the increment angle for rotating the spawn position
            float angleIncrement = 360f / count;
            float angleOfRotation = angleIncrement / 2; // Offset angle so not directly in front of player
            
            for (int i = 0; i < count; i++)
            {
                // Calculate spawn position
                Vector3 spawnPosition = Quaternion.Euler(0, angleOfRotation, 0) * positionToRotate + pivotPosition;
                spawnPosition.y = ichorArmSpawnHeight;
                    
                SpawnIchorArmAtPosition(spawnPosition);

                angleOfRotation += angleIncrement;
            }
        }

        //-///////////////////////////////////////////////////////////
        /// 
        private IchorHazardArmController SpawnIchorArmAtPosition(Vector3 argPosition)
        {
            // If the spawn position is outside the bounds of the volume, don't spawn ichor arm
            if (VolumeContainsPosition(new Vector3(argPosition.x, colliderContainingPlayer.bounds.center.y, argPosition.z)) == false)
            {
                return null;
            }
            
            // Activate the ichor arm and set it to the spawn position
            IchorHazardArmController ichorArm = _ichorArms[_ichorArmIndex];
            ichorArm.transform.position = argPosition;
            ichorArm.gameObject.SetActive(true);
            ichorArm.Emerge();
            
            // Subscribe to ichorArm's onGrabPlayer UnityEvent
            // Causes ichorArm to submerge on grab
            ichorArm.onGrabPlayer.AddListener(() =>
            {
                SubmergePullUnderIchorArms();
                
                // Delay the submergence of the ichor arm that grabbed the player
                this.InvokeAction(() =>
                {
                    ichorArm.Submerge(false);
                }, _submergenceOnGrabDelayLength);
            });

            // Update ichor arm index
            _ichorArmIndex++;
            if (_ichorArmIndex >= _ichorArms.Length)
            {
                _ichorArmIndex = 0;
            }
            return ichorArm;
        }

        //-///////////////////////////////////////////////////////////
        /// 
        /// Instantly deactivate all ichor arms
        /// 
        private void DeactivateAllIchorArms()
        {
            if (_ichorArms == null)
            {
                Debug.LogWarning("Ichor arms are not added to serialized list in IchorArmPoolVolume");
                return;
            }
            
            // Deactivate pool ichor arms
            foreach (IchorHazardArmController ichorArm in _ichorArms)
            {
                ichorArm.gameObject.SetActive(false);
            }

            // Deactivate pull under ichor arms
            _isPullingPlayerUnder = false;
            _pullUnderIchorArms.SetActive(false);
            _pullUnderDistortionEffects.SetActive(false);
        }

        //-///////////////////////////////////////////////////////////
        /// 
        /// Sets pool state to submerged in all ichor arms, and goes
        /// through the movements of submerging
        /// 
        private void SubmergeAllIchorArms()
        {
            SubmergePullUnderIchorArms();
            foreach (IchorHazardArmController ichorArm in _ichorArms)
            {
                if (ichorArm.gameObject.activeSelf == true)
                {
                    ichorArm.Submerge(true);
                    ichorArm.onGrabPlayer.RemoveAllListeners();
                }
            }
        }

        //-///////////////////////////////////////////////////////////
        /// 
        public void SubmergePullUnderIchorArms()
        {
            if (_pullUnderIchorArms.activeSelf)
            {
                _pullUnderAnimator.CrossFade(PullUnder_ForceSubmergeIchorArms_ParamID, 0.15f);
            }
        }

#region Resetting
        //-///////////////////////////////////////////////////////////
        /// 
        public override void ResetObject()
        {
            base.ResetObject();

            // Deactivate game objects
            DeactivateAllIchorArms();

            // Set animation parameters
            _pullUnderAnimator?.SetBool(PullUnder_IsActivated_ParamID, false);
        }
#endregion // Resetting
    }

}


