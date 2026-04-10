using System.Reflection;
using UnityEngine;

namespace Wyrdrasil.Souls.Components
{
    public sealed class WyrdrasilVikingNpcAI : MonsterAI
    {
        private const float SeatApproachRadius = 0.95f;
        private const float BedApproachRadius = 0.95f;
        private const float AttemptRetryInterval = 0.25f;
        private const float ApproachProgressEpsilon = 0.10f;
        private const float ApproachTimeoutDirect = 1.35f;
        private const float ApproachTimeoutFromRoute = 0.75f;
        private const float ApproachStuckTimeoutDirect = 0.90f;
        private const float ApproachStuckTimeoutFromRoute = 0.50f;
        private const float CivilianWalkNativeSpeed = 1.90f;
        private const float CivilianWalkNativeWalkSpeed = 1.60f;
        private const float CivilianWalkNativeRunSpeed = 2.05f;

        private enum NavigationMode
        {
            Idle,
            Steering,
            SeatApproach,
            SeatAttempt,
            Seated,
            BedApproach,
            BedAttempt,
            Sleeping
        }

        private static readonly BindingFlags ReflectionFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private WyrdrasilVikingNpc? _viking;
        private Rigidbody? _rigidbody;
        private Component? _nativeLocomotionOwner;
        private FieldInfo? _nativeSpeedField;
        private FieldInfo? _nativeWalkSpeedField;
        private FieldInfo? _nativeRunSpeedField;
        private FieldInfo? _nativeRunField;
        private FieldInfo? _nativeWalkField;
        private FieldInfo? _nativeRunningField;
        private FieldInfo? _nativeWalkingField;

        private Vector3 _steeringTarget;
        private float _steeringStopDistance;
        private Vector3 _steeringFacingDirection;
        private bool _hasSteeringTarget;

        private Chair? _seatChairComponent;
        private Vector3 _seatUsePosition;
        private Vector3 _seatFacingDirection;
        private Bed? _bedComponent;
        private Transform? _bedAttachPoint;
        private Vector3 _bedUsePosition;
        private Vector3 _bedFacingDirection;
        private bool _travelLocked;

        private Vector3 _approachPoint;
        private float _approachElapsed;
        private float _approachTimeout;
        private float _approachStuckTimer;
        private float _approachStuckTimeout;
        private float _bestApproachDistance;
        private float _nextAttemptTime;
        private bool _useCivilianWalkLocomotion = true;
        private bool _nativeLocomotionDefaultsCaptured;
        private float _defaultNativeSpeed;
        private float _defaultNativeWalkSpeed;
        private float _defaultNativeRunSpeed;
        private bool _defaultNativeRun;
        private bool _defaultNativeWalk;
        private bool _defaultNativeRunning;
        private bool _defaultNativeWalking;
        private NavigationMode _mode = NavigationMode.Idle;

        protected override void Awake()
        {
            base.Awake();
            _viking = GetComponent<WyrdrasilVikingNpc>();
            _rigidbody = GetComponent<Rigidbody>();
            ResolveNativeLocomotionFields();
            CaptureNativeLocomotionDefaults();
            ApplyNativeCivilianWalkMode();
        }

        public void EnterRegistryTravelLock()
        {
            _travelLocked = true;
            _hasSteeringTarget = false;
            ClearSeatTarget();
            ClearBedTarget();
            _mode = NavigationMode.Idle;
            StopMoving();
            ZeroVelocity();
            enabled = false;
        }

        public void ExitRegistryTravelLock()
        {
            _travelLocked = false;
            ClearSteering();
            enabled = true;
        }

        public void SetCivilianWalkLocomotion(bool enabled)
        {
            _useCivilianWalkLocomotion = enabled;
            ApplyNativeCivilianWalkMode();
        }

        public void SetSteeringTarget(Vector3 targetPosition, float stopDistance, Vector3 facingDirection)
        {
            _travelLocked = false;
            _steeringTarget = targetPosition;
            _steeringStopDistance = stopDistance;
            _steeringFacingDirection = facingDirection.sqrMagnitude > 0.0001f
                ? facingDirection.normalized
                : transform.forward;

            ClearSeatTarget();
            ClearBedTarget();
            _hasSteeringTarget = true;
            _mode = NavigationMode.Steering;
            enabled = true;
        }

        public void ClearSteering()
        {
            _hasSteeringTarget = false;
            ClearSeatTarget();
            ClearBedTarget();
            _mode = _viking != null && _viking.IsAttached() ? NavigationMode.Seated : NavigationMode.Idle;
            StopMoving();
            ZeroVelocity();
        }

        public void StartSeatApproach(
            Vector3 approachPosition,
            Vector3 seatUsePosition,
            Vector3 seatFacingDirection,
            Chair? chairComponent,
            bool arrivedFromWaypointRoute = false)
        {
            StartOccupiedAnchorApproach(approachPosition, arrivedFromWaypointRoute);
            _seatChairComponent = chairComponent;
            _seatUsePosition = seatUsePosition;
            _seatFacingDirection = seatFacingDirection.sqrMagnitude > 0.0001f
                ? seatFacingDirection.normalized
                : transform.forward;
            ClearBedTarget();
            _mode = NavigationMode.SeatApproach;
        }

        public void StartBedApproach(
            Vector3 approachPosition,
            Vector3 bedUsePosition,
            Vector3 bedFacingDirection,
            Bed? bedComponent,
            Transform? bedAttachPoint,
            bool arrivedFromWaypointRoute = false)
        {
            StartOccupiedAnchorApproach(approachPosition, arrivedFromWaypointRoute);
            ClearSeatTarget();
            _bedComponent = bedComponent;
            _bedAttachPoint = bedAttachPoint;
            _bedUsePosition = bedUsePosition;
            _bedFacingDirection = bedFacingDirection.sqrMagnitude > 0.0001f
                ? bedFacingDirection.normalized
                : transform.forward;
            _mode = NavigationMode.BedApproach;
        }

        public override bool UpdateAI(float dt)
        {
            if (_viking == null)
            {
                return true;
            }

            if (_travelLocked)
            {
                StopMoving();
                ZeroVelocity();
                return true;
            }

            if (_viking.IsAttached())
            {
                _mode = _bedComponent != null ? NavigationMode.Sleeping : NavigationMode.Seated;
                StopMoving();
                ZeroVelocity();
                return true;
            }

            switch (_mode)
            {
                case NavigationMode.Steering:
                    UpdateSteering(dt);
                    break;

                case NavigationMode.SeatApproach:
                    UpdateSeatApproach(dt);
                    break;

                case NavigationMode.SeatAttempt:
                    UpdateSeatAttempt(dt);
                    break;

                case NavigationMode.BedApproach:
                    UpdateBedApproach(dt);
                    break;

                case NavigationMode.BedAttempt:
                    UpdateBedAttempt(dt);
                    break;

                case NavigationMode.Seated:
                case NavigationMode.Sleeping:
                    StopMoving();
                    ZeroVelocity();
                    break;

                case NavigationMode.Idle:
                default:
                    StopMoving();
                    ZeroVelocity();
                    break;
            }

            return true;
        }

        private void StartOccupiedAnchorApproach(Vector3 approachPoint, bool arrivedFromWaypointRoute)
        {
            _travelLocked = false;
            _approachPoint = approachPoint;
            _approachElapsed = 0f;
            _approachTimeout = arrivedFromWaypointRoute ? ApproachTimeoutFromRoute : ApproachTimeoutDirect;
            _approachStuckTimeout = arrivedFromWaypointRoute ? ApproachStuckTimeoutFromRoute : ApproachStuckTimeoutDirect;
            _approachStuckTimer = 0f;
            _bestApproachDistance = float.MaxValue;
            _nextAttemptTime = 0f;
            _hasSteeringTarget = false;
            enabled = true;
        }

        private void UpdateSteering(float dt)
        {
            if (!_hasSteeringTarget)
            {
                _mode = NavigationMode.Idle;
                StopMoving();
                ZeroVelocity();
                return;
            }

            if (HasReachedHorizontally(_steeringTarget, _steeringStopDistance))
            {
                StopMoving();
                ZeroVelocity();
                RotateTowards(_steeringFacingDirection, dt);
                return;
            }

            ApplyNativeCivilianWalkMode();
            MoveTo(dt, _steeringTarget, _steeringStopDistance, false);
        }

        private void UpdateSeatApproach(float dt)
        {
            if (_seatChairComponent == null)
            {
                _mode = NavigationMode.Idle;
                return;
            }

            if (HasReachedHorizontally(_approachPoint, SeatApproachRadius))
            {
                EnterSeatAttemptMode();
                return;
            }

            RotateBodyTowards(_approachPoint, dt);
            ApplyNativeCivilianWalkMode();
            MoveTo(dt, _approachPoint, SeatApproachRadius, false);
            UpdateApproachProgress(dt, NavigationMode.SeatAttempt, SeatApproachRadius);
        }

        private void UpdateSeatAttempt(float dt)
        {
            if (_seatChairComponent == null || _viking == null)
            {
                _mode = NavigationMode.Idle;
                return;
            }

            StopMoving();
            ZeroVelocity();
            RotateTowards(_seatFacingDirection, dt);

            if (Time.time < _nextAttemptTime)
            {
                return;
            }

            _nextAttemptTime = Time.time + AttemptRetryInterval;
            _seatChairComponent.Interact(_viking, false, false);

            if (_viking.IsAttached())
            {
                _mode = NavigationMode.Seated;
            }
        }

        private void UpdateBedApproach(float dt)
        {
            if (_bedComponent == null || _bedAttachPoint == null)
            {
                _mode = NavigationMode.Idle;
                return;
            }

            if (HasReachedHorizontally(_approachPoint, BedApproachRadius))
            {
                EnterBedAttemptMode();
                return;
            }

            RotateBodyTowards(_approachPoint, dt);
            ApplyNativeCivilianWalkMode();
            MoveTo(dt, _approachPoint, BedApproachRadius, false);
            UpdateApproachProgress(dt, NavigationMode.BedAttempt, BedApproachRadius);
        }

        private void UpdateBedAttempt(float dt)
        {
            if (_bedComponent == null || _bedAttachPoint == null || _viking == null)
            {
                _mode = NavigationMode.Idle;
                return;
            }

            StopMoving();
            ZeroVelocity();
            RotateTowards(_bedFacingDirection, dt);

            if (Time.time < _nextAttemptTime)
            {
                return;
            }

            _nextAttemptTime = Time.time + AttemptRetryInterval;
            _bedComponent.Interact(_viking, false, false);

            if (_viking.IsAttached())
            {
                _mode = NavigationMode.Sleeping;
            }
        }

        private void UpdateApproachProgress(float dt, NavigationMode nextMode, float relaxedRadius)
        {
            var currentDistance = HorizontalDistanceTo(_approachPoint);
            if (currentDistance < _bestApproachDistance - ApproachProgressEpsilon)
            {
                _bestApproachDistance = currentDistance;
                _approachStuckTimer = 0f;
            }
            else
            {
                _approachStuckTimer += dt;
            }

            _approachElapsed += dt;

            if (_approachElapsed >= _approachTimeout ||
                (_approachStuckTimer >= _approachStuckTimeout && HasReachedHorizontally(_approachPoint, relaxedRadius)))
            {
                _nextAttemptTime = 0f;
                _mode = nextMode;
                StopMoving();
                ZeroVelocity();
            }
        }

        private void EnterSeatAttemptMode()
        {
            StopMoving();
            ZeroVelocity();
            _nextAttemptTime = 0f;
            _mode = NavigationMode.SeatAttempt;
        }

        private void EnterBedAttemptMode()
        {
            StopMoving();
            ZeroVelocity();
            _nextAttemptTime = 0f;
            _mode = NavigationMode.BedAttempt;
        }

        private void RotateBodyTowards(Vector3 targetPoint, float dt)
        {
            var direction = targetPoint - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 360f * dt);
        }

        private void RotateTowards(Vector3 facingDirection, float dt)
        {
            var flatDirection = facingDirection;
            flatDirection.y = 0f;

            if (flatDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 360f * dt);
        }

        private bool HasReachedHorizontally(Vector3 targetPoint, float radius)
        {
            return HorizontalDistanceTo(targetPoint) <= radius;
        }

        private float HorizontalDistanceTo(Vector3 targetPoint)
        {
            var delta = targetPoint - transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        private void ResolveNativeLocomotionFields()
        {
            _nativeLocomotionOwner = _viking;
            if (_nativeLocomotionOwner == null)
            {
                return;
            }

            var currentType = _nativeLocomotionOwner.GetType();
            while (currentType != null)
            {
                _nativeSpeedField ??= currentType.GetField("m_speed", ReflectionFlags);
                _nativeWalkSpeedField ??= currentType.GetField("m_walkSpeed", ReflectionFlags);
                _nativeRunSpeedField ??= currentType.GetField("m_runSpeed", ReflectionFlags);
                _nativeRunField ??= currentType.GetField("m_run", ReflectionFlags);
                _nativeWalkField ??= currentType.GetField("m_walk", ReflectionFlags);
                _nativeRunningField ??= currentType.GetField("m_running", ReflectionFlags);
                _nativeWalkingField ??= currentType.GetField("m_walking", ReflectionFlags);
                currentType = currentType.BaseType;
            }
        }

        private void CaptureNativeLocomotionDefaults()
        {
            if (_nativeLocomotionDefaultsCaptured || _nativeLocomotionOwner == null)
            {
                return;
            }

            if (_nativeSpeedField != null && _nativeSpeedField.GetValue(_nativeLocomotionOwner) is float speed)
            {
                _defaultNativeSpeed = speed;
            }

            if (_nativeWalkSpeedField != null && _nativeWalkSpeedField.GetValue(_nativeLocomotionOwner) is float walkSpeed)
            {
                _defaultNativeWalkSpeed = walkSpeed;
            }

            if (_nativeRunSpeedField != null && _nativeRunSpeedField.GetValue(_nativeLocomotionOwner) is float runSpeed)
            {
                _defaultNativeRunSpeed = runSpeed;
            }

            if (_nativeRunField != null && _nativeRunField.GetValue(_nativeLocomotionOwner) is bool run)
            {
                _defaultNativeRun = run;
            }

            if (_nativeWalkField != null && _nativeWalkField.GetValue(_nativeLocomotionOwner) is bool walk)
            {
                _defaultNativeWalk = walk;
            }

            if (_nativeRunningField != null && _nativeRunningField.GetValue(_nativeLocomotionOwner) is bool running)
            {
                _defaultNativeRunning = running;
            }

            if (_nativeWalkingField != null && _nativeWalkingField.GetValue(_nativeLocomotionOwner) is bool walking)
            {
                _defaultNativeWalking = walking;
            }

            _nativeLocomotionDefaultsCaptured = true;
        }

        private void ApplyNativeCivilianWalkMode()
        {
            if (_nativeLocomotionOwner == null)
            {
                return;
            }

            if (!_nativeLocomotionDefaultsCaptured)
            {
                CaptureNativeLocomotionDefaults();
            }

            if (_useCivilianWalkLocomotion)
            {
                if (_nativeSpeedField != null)
                {
                    _nativeSpeedField.SetValue(_nativeLocomotionOwner, CivilianWalkNativeSpeed);
                }

                if (_nativeWalkSpeedField != null)
                {
                    _nativeWalkSpeedField.SetValue(_nativeLocomotionOwner, CivilianWalkNativeWalkSpeed);
                }

                if (_nativeRunSpeedField != null)
                {
                    _nativeRunSpeedField.SetValue(_nativeLocomotionOwner, CivilianWalkNativeRunSpeed);
                }

                if (_nativeRunField != null)
                {
                    _nativeRunField.SetValue(_nativeLocomotionOwner, false);
                }

                if (_nativeWalkField != null)
                {
                    _nativeWalkField.SetValue(_nativeLocomotionOwner, true);
                }

                if (_nativeRunningField != null)
                {
                    _nativeRunningField.SetValue(_nativeLocomotionOwner, false);
                }

                if (_nativeWalkingField != null)
                {
                    _nativeWalkingField.SetValue(_nativeLocomotionOwner, true);
                }

                return;
            }

            if (_nativeSpeedField != null)
            {
                _nativeSpeedField.SetValue(_nativeLocomotionOwner, _defaultNativeSpeed);
            }

            if (_nativeWalkSpeedField != null)
            {
                _nativeWalkSpeedField.SetValue(_nativeLocomotionOwner, _defaultNativeWalkSpeed);
            }

            if (_nativeRunSpeedField != null)
            {
                _nativeRunSpeedField.SetValue(_nativeLocomotionOwner, _defaultNativeRunSpeed);
            }

            if (_nativeRunField != null)
            {
                _nativeRunField.SetValue(_nativeLocomotionOwner, _defaultNativeRun);
            }

            if (_nativeWalkField != null)
            {
                _nativeWalkField.SetValue(_nativeLocomotionOwner, _defaultNativeWalk);
            }

            if (_nativeRunningField != null)
            {
                _nativeRunningField.SetValue(_nativeLocomotionOwner, _defaultNativeRunning);
            }

            if (_nativeWalkingField != null)
            {
                _nativeWalkingField.SetValue(_nativeLocomotionOwner, _defaultNativeWalking);
            }
        }

        private void ZeroVelocity()
        {
            if (_rigidbody == null)
            {
                return;
            }

            _rigidbody.velocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        private void ClearSeatTarget()
        {
            _seatChairComponent = null;
            _seatUsePosition = Vector3.zero;
            _seatFacingDirection = Vector3.forward;
        }

        private void ClearBedTarget()
        {
            _bedComponent = null;
            _bedAttachPoint = null;
            _bedUsePosition = Vector3.zero;
            _bedFacingDirection = Vector3.forward;
        }
    }
}
