using UnityEngine;

public class CollaborationManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BattleManager battleManager;

    public void Init(BattleManager manager)
    {
        battleManager = manager;
    }

    private void Awake()
    {
        if (battleManager == null)
            battleManager = GetComponentInParent<BattleManager>();
    }

    // 다음 단계에서 구현 예정:
    // - 상대 필드 진입 판정
    // - 상대 캐릭터와 합방 시작
    // - 공격/반격
    // - 체력 감소
    // - 휴식존 이동
}