using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardDetailPanel : MonoBehaviour
{
    [Header("Image")]
    public Image cardImage;

    [Header("Basic Info Texts")]
    public TMP_Text cardNameText;
    public TMP_Text cardKindText;
    public TMP_Text cardCharmText;
    public TMP_Text cardHashtagText;

    [Header("Zoom Popup")]
    public GameObject cardZoomPopupPanel;
    public Image zoomCardImage;

    [Header("Effect Text")]
    public TMP_Text cardEffectText;
    
    private void Awake()
    {
        if (cardZoomPopupPanel != null)
            cardZoomPopupPanel.SetActive(false);

        if (cardImage != null)
        {
            Button imageButton = cardImage.GetComponent<Button>();

            if (imageButton == null)
                imageButton = cardImage.gameObject.AddComponent<Button>();

            imageButton.onClick.RemoveAllListeners();
            imageButton.onClick.AddListener(OpenZoomPopup);
        }

        if (cardZoomPopupPanel != null)
        {
            Button popupButton = cardZoomPopupPanel.GetComponent<Button>();

            if (popupButton == null)
                popupButton = cardZoomPopupPanel.AddComponent<Button>();

            popupButton.onClick.RemoveAllListeners();
            popupButton.onClick.AddListener(CloseZoomPopup);
        }
    }
    public void ShowCard(BaseCardData card)
    {
        if (card == null)
        {
            Clear();
            return;
        }

        SetBasicInfo(card);
        SetCardImage(card);
        SetEffectText(card);
    }

    private void SetBasicInfo(BaseCardData card)
    {
        if (cardNameText != null)
            cardNameText.text = card.name;

        if (cardKindText != null)
            cardKindText.text = $"유형: {GetKoreanKind(card.kind)}";

        if (cardCharmText != null)
            cardCharmText.text = $"속성: {ArrayToKoreanCharmText(card.charm)}";

        if (cardHashtagText != null)
            cardHashtagText.text = $"해시태그: {ArrayToText(card.hashtags)}";
    }

    private void SetCardImage(BaseCardData card)
    {
        if (cardImage == null) return;

        Sprite sprite = Resources.Load<Sprite>(card.image);

        if (sprite == null)
        {
            Debug.LogWarning($"상세 카드 이미지를 찾을 수 없습니다: {card.image}");
            cardImage.sprite = null;
            return;
        }

        cardImage.sprite = sprite;
    }

    private void SetEffectText(BaseCardData card)
    {
        if (cardEffectText == null) return;

        StringBuilder sb = new StringBuilder();

        if (card is IdolCardData idol)
        {
            sb.AppendLine("[아이돌 정보]");
            sb.AppendLine($"방송 슬롯: {idol.maxBroadcastSlots}");
            sb.AppendLine($"기본 시청자 획득량: {idol.baseViewersPerPrep}");
            sb.AppendLine($"액티브 코스트: {idol.activeCost}");
            sb.AppendLine();

            AppendEffects(sb, "[패시브 효과]", idol.passive);
            AppendEffects(sb, "[액티브 효과]", idol.active);
        }
        else if (card is BroadcastCardData broadcast)
        {
            sb.AppendLine("[방송 정보]");
            sb.AppendLine($"시청자 보정값: {broadcast.viewersModifier}");
            sb.AppendLine();

            AppendEffects(sb, "[방송 효과]", broadcast.effects);
        }
        else if (card is CharacterCardData character)
        {
            sb.AppendLine("[캐릭터 정보]");
            sb.AppendLine($"출연 코스트: {character.appearCost}");
            sb.AppendLine($"액티브 코스트: {character.activeCost}");
            sb.AppendLine($"합방 텐션: {character.tension}");
            sb.AppendLine($"체력: {character.hpMax}");
            sb.AppendLine();

            AppendEffects(sb, "[캐릭터 효과]", character.effects);
        }
        else if (card is ContentCardData content)
        {
            sb.AppendLine("[콘텐츠 정보]");
            sb.AppendLine($"콘텐츠 타입: {content.contentType}");
            sb.AppendLine($"사용 코스트: {content.cost}");
            sb.AppendLine();

            AppendEffects(sb, "[콘텐츠 효과]", content.effects);
        }

        cardEffectText.text = sb.ToString();
    }

    private void AppendEffects(StringBuilder sb, string title, EffectData[] effects)
    {
        sb.AppendLine(title);

        if (effects == null || effects.Length == 0)
        {
            sb.AppendLine("효과 없음");
            sb.AppendLine();
            return;
        }

        foreach (EffectData effect in effects)
        {
            if (effect == null) continue;

            if (!string.IsNullOrEmpty(effect.timing))
                sb.AppendLine($"[{GetKoreanTiming(effect.timing)}]");

            if (!string.IsNullOrEmpty(effect.description))
                sb.AppendLine(effect.description);

            sb.AppendLine();
        }
    }

    private string ArrayToText(string[] values)
    {
        if (values == null || values.Length == 0)
            return "-";

        return string.Join(", ", values);
    }

    public void OpenZoomPopup()
    {
        if (cardImage == null || cardImage.sprite == null)
            return;

        if (cardZoomPopupPanel == null || zoomCardImage == null)
            return;

        zoomCardImage.sprite = cardImage.sprite;
        zoomCardImage.preserveAspect = true;

        cardZoomPopupPanel.SetActive(true);
    }

    public void CloseZoomPopup()
    {
        if (cardZoomPopupPanel != null)
            cardZoomPopupPanel.SetActive(false);
    }
    
    public void Clear()
    {
        if (cardImage != null)
            cardImage.sprite = null;

        if (cardNameText != null)
            cardNameText.text = "";

        if (cardKindText != null)
            cardKindText.text = "";

        if (cardCharmText != null)
            cardCharmText.text = "";

        if (cardHashtagText != null)
            cardHashtagText.text = "";

        if (cardEffectText != null)
            cardEffectText.text = "";
            
        CloseZoomPopup();
    }

    private string GetKoreanKind(string kind)
{
    switch (kind)
    {
        case "Idol":
            return "아이돌";
        case "Character":
            return "캐릭터";
        case "Content":
            return "컨텐츠";
        case "Broadcast":
            return "방송";
        default:
            return kind;
    }
}

private string GetKoreanCharm(string charm)
{
    switch (charm)
    {
        case "Lovely":
            return "러블리";
        case "Tricky":
            return "트리키";
        case "Pure":
            return "청초";
        case "Cool":
            return "쿨";
        case "Free":
            return "프리";
        default:
            return charm;
    }
}

    private string ArrayToKoreanCharmText(string[] values)
{
    if (values == null || values.Length == 0)
        return "-";

        string[] koreanValues = new string[values.Length];

        for (int i = 0; i < values.Length; i++)
        {
        koreanValues[i] = GetKoreanCharm(values[i]);
        }

    return string.Join(", ", koreanValues);
}

private string GetKoreanTiming(string timing)
{
    switch (timing)
    {
        case "Passive":
            return "상시";

        case "Active":
            return "액티브";

        case "Content":
            return "콘텐츠";

        case "OnAppear":
            return "출연";

        case "Rest":
            return "휴식";

        case "Always":
            return "상시";

        case "PreCollab":
            return "합방 전";

        case "PostCollab":
            return "합방 후";

        case "Collab":
            return "합방";

        default:
            return timing;
    }
}
}