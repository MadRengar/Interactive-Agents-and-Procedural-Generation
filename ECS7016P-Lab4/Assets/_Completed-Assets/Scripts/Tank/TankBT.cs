using UnityEngine;
using NPBehave;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Tree;

namespace Complete
{
    /*
    A simple tank AI based on behaviour trees.
    */
    public class TankBT : MonoBehaviour
    {
        public int m_PlayerNumber = 1;      // Used to identify which tank belongs to which player.  This is set by this tank's manager.
        public int m_Behaviour = 0;         // Used to select an AI behaviour in the Unity Inspector

        private TankMovement m_Movement;    // Reference to tank's movement script, used by the AI to control movement.
        private TankShooting m_Shooting;    // Reference to tank's shooting script, used by the AI to fire shells.
        private TankHealth m_Health;
        private List<GameObject> m_Targets; // List of enemy targets for this tank
        private Root tree;                  // The tank's behaviour tree
        private Blackboard blackboard;      // The tank's behaviour blackboard

        // Initialisation
        private void Awake()
        {
            m_Targets = new List<GameObject>();
        }

        // Start behaviour tree
        private void Start()
        {
            Debug.Log("Initialising AI player " + m_PlayerNumber);
            m_Movement = GetComponent<TankMovement>();
            m_Shooting = GetComponent<TankShooting>();
            m_Health = GetComponent<TankHealth>();

            //tree = CreateBehaviourTree();
            tree = SelectBehaviourTree();
            blackboard = tree.Blackboard;
#if UNITY_EDITOR
            Debugger debugger = (Debugger)this.gameObject.AddComponent(typeof(Debugger));
            debugger.BehaviorTree = tree;
#endif

            tree.Start();
        }

        private void Update()
        {
            // 每帧重新计算 Utility 并选择行为（可选，防止 AI 行为固定不变）
            tree.Stop();
            tree = SelectBehaviourTree();
            tree.Start();
        }

        private Root CreateBehaviourTree()
        {
            // To change a tank's behaviour:
            // - Examine the GameManager object in the inspector.
            // - Open the Tanks list, find the tank's entry.
            // - Enable the NPC checkbox.
            // - Edit the Behaviour field to an integer N corresponding to the behaviour below
            switch (m_Behaviour)
            {
                // N=1
                case 1:
                    return SpinBehaviour(-0.05f, 1f);
                // N=2
                case 2:
                    return TrackAndFireBehaviour();
                // N=3
                case 3:
                    return EngageBehaviour();

                // Default behaviour: turn slowly
                default:
                    return TurnSlowly();
            }
        }

        /**************************************
         * 
         * TANK Basic ACTIONS
         * 
         * Move, turn and fire
         */

        // ACTION: move the tank with a velocity between -1 and 1.
        // -1: fast reverse
        // 0: no change
        // 1: fast forward
        private void Move(float velocity)
        {
            m_Movement.AIMove(velocity);
        }

        // ACTION: turn the tank with angular velocity between -1 and 1.
        // -1: fast turn left
        // 0: no change
        // 1: fast turn right
        private void Turn(float velocity)
        {
            m_Movement.AITurn(velocity);
        }

        // ACTION: fire a shell with a force between 0 and 1
        // 0: minimum force
        // 1: maximum force
        private void Fire(float force)
        {
            m_Shooting.AIFire(force);
        }


        /**************************************
        * 
        * TANK PERCEPTION
        * 
        */

        private void UpdatePerception()
        {
            Vector3 targetPos = TargetTransform().position;
            Vector3 localPos = this.transform.InverseTransformPoint(targetPos);
            Vector3 heading = localPos.normalized;
            blackboard["targetDistance"] = localPos.magnitude;
            blackboard["targetInFront"] = heading.z > 0;
            blackboard["targetOnRight"] = heading.x > 0;
            blackboard["targetOffCentre"] = Mathf.Abs(heading.x);
        }

        // Register an enemy target 
        public void AddTarget(GameObject target)
        {
            m_Targets.Add(target);
        }

        // Get the transform for the first target
        private Transform TargetTransform()
        {
            if (m_Targets.Count > 0)
            {
                return m_Targets[0].transform;
            }
            else
            {
                return null;
            }
        }


        private Root SelectBehaviourTree()
        {
            float attackUtility = GetAttackUtility();
            float fleeUtility = GetFleeUtility();
            float huntUtility = GetHuntUtility();

            if (attackUtility >= fleeUtility && attackUtility >= huntUtility)
            {
                Debug.Log("攻击行为");
                return TrackAndFireBehaviour();
            }
            else if (fleeUtility >= attackUtility && fleeUtility >= huntUtility)
            {
                Debug.Log("逃跑行为");
                return FleeBehaviour();
            }
            else
            {
                Debug.Log("追猎行为");
                return HuntBehaviour();
            }
        }

        /**************************************
         * 
         * Utility Agent
         */
        private float GetAttackUtility()
        {
            if (m_Targets.Count == 0) return 0f;
            float distance = Vector3.Distance(transform.position, m_Targets[0].transform.position);
            return Mathf.Clamp01(1.0f - distance / 50.0f);
        }

        private float GetFleeUtility()
        {
            return Mathf.Clamp01(1.0f - (m_Health.GetCurrentHealth() / m_Health.m_StartingHealth));
        }

        private float GetHuntUtility()
        {
            if (m_Targets.Count == 0) return 1.0f;
            float distance = Vector3.Distance(transform.position, m_Targets[0].transform.position);
            return Mathf.Clamp01(distance / 50.0f);
        }


        /*FleeBehaviour()——逃跑行为*/
        private Root FleeBehaviour()
        {
            return new Root(new Sequence(new Action(() => Move(-1.0f)), new Wait(2.0f)));
        }
        /*HuntBehaviour()——追猎行为*/
        private Root HuntBehaviour()
        {
            return new Root(new Sequence(new Action(() => Move(1.0f)), new Wait(2.0f)));
        }

        /*FiringBehaviour()——开火行为*/




        /**************************************
         * 
         * BEHAVIOUR TREE
         */


        /*** BEHAVIOUR TREE 1) SpinBehaviour ***/

        // Constantly spin and fire on the spot 
        private Root SpinBehaviour(float turn, float shoot)
        {
            return new Root(new Sequence(
                        new Action(() => Turn(turn)),
                        new Action(() => Fire(shoot))
                    ));
        }

        // Just turn slowly
        private Root TurnSlowly()
        {
            return new Root(new Action(() => Turn(0.1f)));
        }

        /*** BEHAVIOUR TREE 2) TrackAndFireBehaviour ***/

        // Turn to face your opponent and fire
        private Root TrackAndFireBehaviour()
        {
            // Either fire on target, turn right toward target, or turn left
            Node sel = new Selector(FiringBehaviour(),
                                    TrackTargetOnRight(),
                                    TurnLeft());
            // Wrap behaviour in blackboard update service
            Node service = new Service(0.2f, UpdatePerception, sel);
            Root root = new Root(service);
            return root;
        }

        // Fire on a target if we're facing it
        private Node FiringBehaviour()
        {
            Node seq = new Sequence(StopTurning(),
                                       new Wait(2f),
                                       RandomFire());
            // Check the blackboard: are we facing it?
            // (targetOffCentre =< 0.1)
            Node bb = new BlackboardCondition("targetOffCentre",
                                              Operator.IS_SMALLER_OR_EQUAL, 0.1f,
                                              Stops.IMMEDIATE_RESTART,
                                              seq);
            return bb;
        }

        // Stop the tank from turning
        private Node StopTurning()
        {
            return new Action(() => Turn(0));
        }

        // Fire with a random power
        private Node RandomFire()
        {
            return new Action(() => Fire(UnityEngine.Random.Range(0.0f, 1.0f)));
        }

        // Turn towards a target on our right
        private Node TrackTargetOnRight()
        {
            // Turn right
            Node turn = new Action(() => Turn(0.2f));
            // Check blackboard first: is target on right?
            Node bb = new BlackboardCondition("targetOnRight",
                                              Operator.IS_EQUAL, true,
                                              Stops.IMMEDIATE_RESTART,
                                              turn);
            return bb;
        }

        // Turn left
        private Node TurnLeft()
        {
            return new Action(() => Turn(-0.2f));
        }

        /*** BEHAVIOUR TREE 3) EngageBehaviour ***/

        private Root EngageBehaviour()
        {
            // Either fire/move on target, turn right toward target, or turn left
            Node sel = new Selector(FireAndMoveBehaviour(),
                                    TrackTargetOnRight(),
                                    TurnLeft());
            // Stop tank before doing anything
            Node seq = new Sequence(SetVelocity(0), StopTurning(), sel);
            // Wrap behaviour in blackboard update service
            Node service = new Service(0.2f, UpdatePerception, seq);
            Root root = new Root(service);
            return root;
        }

        // Fire on or move toward a target if we're facing it
        private Node FireAndMoveBehaviour()
        {
            Node seq = new Sequence(RandomMove(), RandomFire());
            // Check the blackboard: are we facing it?
            // (targetOffCentre =< 0.1)
            Node bb = new BlackboardCondition("targetOffCentre",
                                              Operator.IS_SMALLER_OR_EQUAL, 0.1f,
                                              Stops.IMMEDIATE_RESTART,
                                              seq);
            return bb;
        }

        // Set the tank's velocity
        private Node SetVelocity(float velocity)
        {
            return new Action(() => Move(velocity));
        }

        // Move forward at full speed for a random time
        private Node RandomMove()
        {
            float waitTime = UnityEngine.Random.Range(0.1f, 0.3f);
            return new Sequence(SetVelocity(1f),
                                new Wait(waitTime),
                                SetVelocity(0));
        }

    }
}