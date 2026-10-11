import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mockNuxtImport } from '@nuxt/test-utils/runtime'
import { CalendarDate } from '@internationalized/date'
import { UCalendar, UInputMenu, UInputNumber, URadioGroup, USelect, USelectMenu } from '#components'
import AccountEditDialog from '~/components/admin/AccountEditDialog.vue'
import AccountSlotsEditor from '~/components/admin/AccountSlotsEditor.vue'
import AdminAccessSettingsDialog from '~/components/admin/AdminAccessSettingsDialog.vue'
import AdminTeamEventDialog from '~/components/admin/AdminTeamEventDialog.vue'
import AidActivityFilter from '~/components/admin/AidActivityFilter.vue'
import CancelDealDialog from '~/components/admin/CancelDealDialog.vue'
import CreateDealDialog from '~/components/admin/CreateDealDialog.vue'
import DealSlotPicker from '~/components/admin/DealSlotPicker.vue'
import ResolveFlagDialog from '~/components/admin/ResolveFlagDialog.vue'
import { adminAccount, adminDeal, apiError, apiOk, dealFlag, slot } from '../helpers/fixtures'
import { bodyText, button, buttons, click, inputByPlaceholder, mountInApp, toasts, type } from '../helpers/dom'

// Only AccountEditDialog calls the API directly; everything else goes through stores, which are spied on
const { apiFetch } = vi.hoisted(() => ({ apiFetch: vi.fn() }))
mockNuxtImport('useApiClient', () => () => ({ apiFetch }))

beforeEach(() => {
  vi.spyOn(console, 'error').mockImplementation(() => {})
  vi.spyOn(console, 'log').mockImplementation(() => {})
})

// Dialogs are opened through useOverlay in the app; open: true renders them the same way
const openDialog = (component: Parameters<typeof mountInApp>[0], props: Record<string, unknown> = {}) => {
  const onClose = vi.fn()
  return mountInApp(component, { open: true, onClose, ...props }).then((wrapper) => ({ wrapper, onClose }))
}

describe('CancelDealDialog', () => {
  it('requires a reason, then cancels the deal with it', async () => {
    const cancelDeal = vi.spyOn(useAdminDealsStore(), 'cancelDeal').mockResolvedValue()
    const { onClose } = await openDialog(CancelDealDialog, { deal: adminDeal({ id: 10 }) })

    expect(bodyText()).toContain('This ends the deal between Ruler1 and Ruler2')
    expect(button('Cancel Deal').disabled).toBe(true)
    await type(inputByPlaceholder('Why is this deal being cancelled?'), '  Partner left VEP  ')
    await click(button('Cancel Deal'))

    expect(cancelDeal).toHaveBeenCalledWith(10, 'Partner left VEP')
    expect(toasts()[0]).toMatchObject({ color: 'success', description: 'Deal #10 cancelled.' })
    expect(onClose).toHaveBeenCalledWith(true)
  })

  it('stays open with an error when cancelling fails', async () => {
    vi.spyOn(useAdminDealsStore(), 'cancelDeal').mockRejectedValue(new Error('down'))
    const { onClose } = await openDialog(CancelDealDialog, { deal: adminDeal() })

    await type(inputByPlaceholder('Why is this deal being cancelled?'), 'x')
    await click(button('Cancel Deal'))

    expect(toasts()[0]).toMatchObject({ color: 'error' })
    expect(onClose).not.toHaveBeenCalled()
  })

  it('closes without changes from Keep Deal', async () => {
    const cancelDeal = vi.spyOn(useAdminDealsStore(), 'cancelDeal')
    const { onClose } = await openDialog(CancelDealDialog, { deal: adminDeal() })

    await click(button('Keep Deal'))

    expect(onClose).toHaveBeenCalledWith(false)
    expect(cancelDeal).not.toHaveBeenCalled()
  })
})

describe('ResolveFlagDialog', () => {
  it('resolves the flag with the notes', async () => {
    const resolveFlag = vi.spyOn(useAdminDealsStore(), 'resolveFlag').mockResolvedValue()
    const { onClose } = await openDialog(ResolveFlagDialog, { flag: dealFlag({ id: 5, dealId: 10 }) })

    expect(bodyText()).toContain('Resolve Flag on Deal #10')
    await type(inputByPlaceholder('Optional notes on how this was resolved'), ' Partner was on vacation ')
    await click(button('Resolve'))

    expect(resolveFlag).toHaveBeenCalledWith(5, 'Partner was on vacation')
    expect(toasts()[0]).toMatchObject({ color: 'success', description: 'Flag resolved.' })
    expect(onClose).toHaveBeenCalledWith(true)
  })

  it('stays open with an error when resolving fails', async () => {
    vi.spyOn(useAdminDealsStore(), 'resolveFlag').mockRejectedValue(new Error('down'))
    const { onClose } = await openDialog(ResolveFlagDialog, { flag: dealFlag() })

    await click(button('Resolve'))

    expect(toasts()[0]).toMatchObject({ color: 'error' })
    expect(onClose).not.toHaveBeenCalled()
  })

  it('closes without changes from Cancel', async () => {
    const { onClose } = await openDialog(ResolveFlagDialog, { flag: dealFlag() })

    await click(button('Cancel'))

    expect(onClose).toHaveBeenCalledWith(false)
  })
})

describe('DealSlotPicker', () => {
  const accounts = [adminAccount(1), adminAccount(2, { rulerName: 'Sue', nationName: 'Sellland' })]

  it("loads the chosen account's open slots, and picks one with its account attached", async () => {
    const loadSlots = vi.spyOn(useAdminDealsStore(), 'loadAvailableSlots')
      .mockResolvedValue([slot(7, { tech: 1 }, { slotNumber: 3, cashReceivedCycles: 1, account: undefined as never })])
    const onUpdate = vi.fn()
    const wrapper = await mountInApp(DealSlotPicker, { label: 'First Account', accounts, 'onUpdate:modelValue': onUpdate })

    const accountSelect = wrapper.findComponent(USelectMenu)
    expect((accountSelect.props('items') as { label: string }[]).map((i) => i.label)).toEqual(['Ruler1 of Nation1', 'Sue of Sellland'])
    accountSelect.vm.$emit('update:modelValue', 2)
    await flushPromises()

    expect(loadSlots).toHaveBeenCalledWith(2)
    const slotSelect = wrapper.findAllComponents(USelectMenu)[1]
    expect((slotSelect.props('items') as { label: string }[])[0].label).toBe('Slot #3: sends 100 tech, receives $9M')
    slotSelect.vm.$emit('update:modelValue', 7)
    await flushPromises()

    expect(onUpdate).toHaveBeenLastCalledWith(expect.objectContaining({
      id: 7,
      account: { accountId: 2, nationId: 102, nationName: 'Sellland', rulerName: 'Sue', allianceName: 'Valyria' },
    }))
  })

  it('says when the account has no open slots', async () => {
    vi.spyOn(useAdminDealsStore(), 'loadAvailableSlots').mockResolvedValue([])
    const wrapper = await mountInApp(DealSlotPicker, { label: 'First Account', accounts })

    wrapper.findComponent(USelectMenu).vm.$emit('update:modelValue', 1)
    await flushPromises()

    expect(wrapper.text()).toContain('This account has no open slots.')
  })

  it("shows an error when the slots can't be loaded", async () => {
    vi.spyOn(useAdminDealsStore(), 'loadAvailableSlots').mockRejectedValue(new Error('down'))
    const wrapper = await mountInApp(DealSlotPicker, { label: 'First Account', accounts })

    wrapper.findComponent(USelectMenu).vm.$emit('update:modelValue', 1)
    await flushPromises()

    expect(toasts()[0]).toMatchObject({ color: 'error', description: 'Failed to load open slots for that account.' })
  })

  it('clears the slot when the account is cleared', async () => {
    const loadSlots = vi.spyOn(useAdminDealsStore(), 'loadAvailableSlots').mockResolvedValue([slot(7)])
    const onUpdate = vi.fn()
    const wrapper = await mountInApp(DealSlotPicker, { label: 'First Account', accounts, 'onUpdate:modelValue': onUpdate })
    wrapper.findComponent(USelectMenu).vm.$emit('update:modelValue', 1)
    await flushPromises()
    wrapper.findAllComponents(USelectMenu)[1].vm.$emit('update:modelValue', 7)
    await flushPromises()

    wrapper.findComponent(USelectMenu).vm.$emit('update:modelValue', undefined)
    await flushPromises()

    expect(loadSlots).toHaveBeenCalledOnce()
    expect(onUpdate).toHaveBeenLastCalledWith(null)
  })
})

describe('CreateDealDialog', () => {
  const buyerSlot = slot(1, { cash: 1 }, { techReceivedCycles: 1, account: { accountId: 1, nationId: 101, nationName: 'Buyland', rulerName: 'Bob', allianceName: 'V' } })
  const sellerSlot = slot(2, { tech: 1 }, { cashReceivedCycles: 1, account: { accountId: 2, nationId: 102, nationName: 'Sellland', rulerName: 'Sue', allianceName: 'V' } })

  const pick = async (wrapper: Awaited<ReturnType<typeof mountInApp>>, first: unknown, second: unknown) => {
    const pickers = wrapper.findAllComponents(DealSlotPicker)
    pickers[0].vm.$emit('update:modelValue', first)
    pickers[1].vm.$emit('update:modelValue', second)
    await flushPromises()
  }

  it('creates a deal between two matching slots', async () => {
    const createDeal = vi.spyOn(useAdminDealsStore(), 'createDeal').mockResolvedValue()
    const { wrapper, onClose } = await openDialog(CreateDealDialog, { accounts: [adminAccount(1), adminAccount(2)] })
    expect(button('Create Deal').disabled).toBe(true)

    await pick(wrapper, buyerSlot, sellerSlot)

    expect(bodyText()).toContain('Bob sends $9M; Sue sends 100 tech')
    await click(button('Create Deal'))
    expect(createDeal).toHaveBeenCalledWith(1, 2)
    expect(toasts()[0]).toMatchObject({ color: 'success', description: 'Deal created.' })
    expect(onClose).toHaveBeenCalledWith(true)
  })

  it("warns when the slots don't line up, but still allows the deal", async () => {
    const { wrapper } = await openDialog(CreateDealDialog, { accounts: [] })

    await pick(wrapper, buyerSlot, { ...sellerSlot, techSentCycles: 2 })

    expect(bodyText()).toContain("These slots aren't an exact match")
    expect(button('Create Deal').disabled).toBe(false)
  })

  it('refuses a deal between two slots of the same account', async () => {
    const { wrapper } = await openDialog(CreateDealDialog, { accounts: [] })

    await pick(wrapper, buyerSlot, { ...sellerSlot, account: buyerSlot.account })

    expect(bodyText()).toContain('Both slots belong to the same account')
    expect(button('Create Deal').disabled).toBe(true)
  })

  it('stays open with an error when creating fails', async () => {
    vi.spyOn(useAdminDealsStore(), 'createDeal').mockRejectedValue(new Error('duplicate'))
    const { wrapper, onClose } = await openDialog(CreateDealDialog, { accounts: [] })
    await pick(wrapper, buyerSlot, sellerSlot)

    await click(button('Create Deal'))

    expect(toasts()[0]).toMatchObject({ color: 'error' })
    expect(onClose).not.toHaveBeenCalled()
  })

  it('closes without changes from Cancel', async () => {
    const { onClose } = await openDialog(CreateDealDialog, { accounts: [] })

    await click(button('Cancel'))

    expect(onClose).toHaveBeenCalledWith(false)
  })
})

describe('AccountSlotsEditor', () => {
  const slots = () => [slot(1, { cash: 1 }, { slotNumber: 1 }), slot(2, { cash: 1 }, { slotNumber: 2, hasActiveDeal: false })]

  it('lists the slots, and only saves one that was changed', async () => {
    const store = useAdminAccountsStore()
    vi.spyOn(store, 'loadAccountSlots').mockResolvedValue(slots())
    const updateSlot = vi.spyOn(store, 'updateSlot').mockResolvedValue()
    const wrapper = await mountInApp(AccountSlotsEditor, { accountId: 4 })

    expect(store.loadAccountSlots).toHaveBeenCalledWith(4)
    expect(wrapper.text()).toContain('Slot #1In a deal')
    expect(wrapper.text()).toContain('Slot #2Open')
    const saveButtons = () => wrapper.findAll('button').filter((b) => b.text() === 'Save')
    expect(saveButtons().map((b) => b.attributes('disabled'))).toEqual(['', ''])

    wrapper.findAllComponents(UInputNumber)[4].vm.$emit('update:modelValue', 2)
    await flushPromises()
    expect(saveButtons()[1].attributes('disabled')).toBeUndefined()
    await click(saveButtons()[1].element as HTMLElement)

    expect(updateSlot).toHaveBeenCalledWith(expect.objectContaining({ id: 2, techSentCycles: 2 }))
    expect(toasts()[0]).toMatchObject({ color: 'success', description: 'Slot #2 updated.' })
    expect(saveButtons()[1].attributes('disabled')).toBe('')
  })

  it('keeps the change unsaved when saving fails', async () => {
    const store = useAdminAccountsStore()
    vi.spyOn(store, 'loadAccountSlots').mockResolvedValue(slots())
    vi.spyOn(store, 'updateSlot').mockRejectedValue(new Error('down'))
    const wrapper = await mountInApp(AccountSlotsEditor, { accountId: 4 })

    wrapper.findAllComponents(UInputNumber)[0].vm.$emit('update:modelValue', 3)
    await flushPromises()
    const save = wrapper.findAll('button').filter((b) => b.text() === 'Save')[0]
    await click(save.element as HTMLElement)

    expect(toasts()[0]).toMatchObject({ color: 'error' })
    expect(save.attributes('disabled')).toBeUndefined()
  })

  it('says when the account has no slots', async () => {
    vi.spyOn(useAdminAccountsStore(), 'loadAccountSlots').mockResolvedValue([])
    const wrapper = await mountInApp(AccountSlotsEditor, { accountId: 4 })

    expect(wrapper.text()).toContain('This account has no slots.')
  })

  it("shows an error when the slots can't be loaded", async () => {
    vi.spyOn(useAdminAccountsStore(), 'loadAccountSlots').mockRejectedValue(new Error('down'))
    await mountInApp(AccountSlotsEditor, { accountId: 4 })

    expect(toasts()[0]).toMatchObject({ color: 'error', description: "Failed to load this account's slots." })
  })
})

describe('AccountEditDialog', () => {
  const prospect = { nationId: 123, nationName: 'Newland', rulerName: 'Nia', allianceName: 'Valyria' }

  describe('creating an account', () => {
    it('finds the nation, then creates the account', async () => {
      apiFetch.mockResolvedValue(apiOk(prospect))
      const createAccount = vi.spyOn(useAdminAccountsStore(), 'createAccount').mockResolvedValue()
      const { wrapper, onClose } = await openDialog(AccountEditDialog, { account: {} })

      expect(bodyText()).toContain('New Account')
      expect(bodyText()).not.toContain('Secret Code')
      await type(inputByPlaceholder("The account's NationID"), '123')
      await click(button('Search'))

      expect(apiFetch).toHaveBeenCalledWith('/api/accounts/prospect?nationId=123')
      expect(bodyText()).toContain('Secret Code')
      wrapper.findComponent(USelectMenu).vm.$emit('update:modelValue', 'S')
      // FAC and DRA have to be picked explicitly today (see the skipped test below)
      const [fm, fac, dra] = wrapper.findAllComponents(URadioGroup)
      fm.vm.$emit('update:modelValue', 'Y')
      fac.vm.$emit('update:modelValue', 'N')
      dra.vm.$emit('update:modelValue', 'N')
      await click(button('Generate'))
      await type(inputByPlaceholder('e.g. 318737615257993226'), '318737615257993226')
      await click(button('Create'))

      expect(createAccount).toHaveBeenCalledOnce()
      const payload = createAccount.mock.calls[0][0]
      expect(payload).toMatchObject({
        nationId: 123, rulerName: 'Nia', role: 'S', hasForeignMinistry: true, hasFederalAidCommission: false,
        hasDisasterReliefAgency: false, discord: null, discordUniqueId: 318737615257993226n,
      })
      expect(payload.uniqueCode).toMatch(/^\d{5}$/)
      expect(toasts()[0]).toMatchObject({ color: 'success', description: 'Account created successfully!' })
      expect(onClose).toHaveBeenCalledWith(false)
    })

    it('stays open with an error when creating fails', async () => {
      apiFetch.mockResolvedValue(apiOk(prospect))
      vi.spyOn(useAdminAccountsStore(), 'createAccount').mockRejectedValue(new Error('down'))
      const { wrapper, onClose } = await openDialog(AccountEditDialog, { account: {} })
      await type(inputByPlaceholder("The account's NationID"), '123')
      await click(button('Search'))
      wrapper.findAllComponents(URadioGroup).forEach((radio) => radio.vm.$emit('update:modelValue', 'N'))
      await click(button('Generate'))

      await click(button('Create'))

      expect(toasts()[0]).toMatchObject({ color: 'error' })
      expect(onClose).not.toHaveBeenCalled()
    })

    // The FM/FAC/DRA radios show "No" for a new account, but the value behind them stays undefined, which
    // the schema rejects. Unless the admin clicks each one, Create fails with a "Validation Error" toast
    it.skip('BUG: creates an account with the improvements left at their default of No', async () => {
      apiFetch.mockResolvedValue(apiOk(prospect))
      const createAccount = vi.spyOn(useAdminAccountsStore(), 'createAccount').mockResolvedValue()
      const { wrapper } = await openDialog(AccountEditDialog, { account: {} })
      await type(inputByPlaceholder("The account's NationID"), '123')
      await click(button('Search'))
      wrapper.findComponent(USelectMenu).vm.$emit('update:modelValue', 'B')
      await click(button('Generate'))

      await click(button('Create'))

      expect(createAccount).toHaveBeenCalledWith(expect.objectContaining({
        hasForeignMinistry: false, hasFederalAidCommission: false, hasDisasterReliefAgency: false,
      }))
    })

    // findProspectAccount shows the error toast but then reads data.value.nationName from a null response,
    // throwing a TypeError: the dialog stays on "Search" with the button spinning forever. Searching for a
    // nation that isn't in CN (404) or already has an account (409) both hit this
    it.skip('BUG: recovers when the nation search fails', async () => {
      apiFetch.mockResolvedValue(apiError({ statusCode: 409, message: 'Conflict' }))
      await openDialog(AccountEditDialog, { account: {} })
      await type(inputByPlaceholder("The account's NationID"), '123')

      await click(button('Search'))

      expect(toasts()[0]).toMatchObject({ color: 'error' })
      expect(button('Search').disabled).toBe(false)
    })
  })

  describe('editing an account', () => {
    // The slots editor has its own Save buttons; the dialog's is last, in the footer
    const saveAccount = () => click(buttons('Save').at(-1)!)

    beforeEach(() => {
      vi.spyOn(useAdminAccountsStore(), 'loadAccountSlots').mockResolvedValue([slot(1)])
    })

    it('shows the account with its slots, and saves changes', async () => {
      const updateAccount = vi.spyOn(useAdminAccountsStore(), 'updateAccount').mockResolvedValue()
      const { wrapper, onClose } = await openDialog(AccountEditDialog, { account: adminAccount(1, { discord: 'old', discordUniqueId: 5n }) })

      expect(bodyText()).toContain("Ruler1's Account")
      expect(bodyText()).toContain('(1 / 4 in deals)')
      expect(useAdminAccountsStore().loadAccountSlots).toHaveBeenCalledWith(1)
      await type(inputByPlaceholder('e.g. username or username#1234'), '')
      await type(inputByPlaceholder('e.g. 318737615257993226'), 'not a number')
      wrapper.findAllComponents(URadioGroup)[2].vm.$emit('update:modelValue', 'Y')
      await saveAccount()

      expect(updateAccount).toHaveBeenCalledWith(expect.objectContaining({ id: 1, discord: null, discordUniqueId: null, hasDisasterReliefAgency: true }))
      expect(toasts()[0]).toMatchObject({ color: 'success', description: 'Account updated successfully!' })
      expect(onClose).toHaveBeenCalledWith(false)
    })

    it('stays open with an error when saving fails', async () => {
      vi.spyOn(useAdminAccountsStore(), 'updateAccount').mockRejectedValue(new Error('down'))
      const { onClose } = await openDialog(AccountEditDialog, { account: adminAccount(1) })

      await saveAccount()

      expect(toasts()[0]).toMatchObject({ color: 'error' })
      expect(onClose).not.toHaveBeenCalled()
    })

    it('rejects an empty unique code', async () => {
      const updateAccount = vi.spyOn(useAdminAccountsStore(), 'updateAccount')
      await openDialog(AccountEditDialog, { account: adminAccount(1) })

      const uniqueCode = Array.from(document.querySelectorAll<HTMLInputElement>('input')).find((input) => input.value === '12345')!
      await type(uniqueCode, '')
      await saveAccount()

      expect(updateAccount).not.toHaveBeenCalled()
      expect(toasts()[0]).toMatchObject({ title: 'Validation Error' })
      expect(toasts()[0].description).toContain('Every account must have a unique code provided')
    })

    it('closes without saving from Cancel', async () => {
      const updateAccount = vi.spyOn(useAdminAccountsStore(), 'updateAccount')
      const { onClose } = await openDialog(AccountEditDialog, { account: adminAccount(1) })

      await click(button('Cancel'))

      expect(onClose).toHaveBeenCalledWith(false)
      expect(updateAccount).not.toHaveBeenCalled()
    })

    // The dialog edits the account object it's given (reactive(props.account)), which is the row in the
    // accounts list, so changes show in the list even when the admin cancels
    it.skip("BUG: cancelling doesn't change the account in the list", async () => {
      const store = useAdminAccountsStore()
      store.accounts = [adminAccount(1, { discord: 'old' })]
      await openDialog(AccountEditDialog, { account: store.accounts[0] })

      await type(inputByPlaceholder('e.g. username or username#1234'), 'changed')
      await click(button('Cancel'))

      expect(store.accounts[0].discord).toBe('old')
    })
  })
})

describe('AdminAccessSettingsDialog', () => {
  it('loads accounts to choose from, then grants the chosen role', async () => {
    const accountsStore = useAdminAccountsStore()
    vi.spyOn(accountsStore, 'loadAllAccounts').mockImplementation(async () => {
      accountsStore.accounts = [adminAccount(1)]
    })
    const createAccess = vi.spyOn(useAdminAccessStore(), 'createAdminAccess').mockResolvedValue(true)
    const { wrapper, onClose } = await openDialog(AdminAccessSettingsDialog, { editingAdmin: {} })

    expect(accountsStore.loadAllAccounts).toHaveBeenCalledWith(false)
    expect(bodyText()).toContain('Add Admin Access')
    wrapper.findComponent(UInputMenu).vm.$emit('update:modelValue', 1)
    wrapper.findComponent(USelectMenu).vm.$emit('update:modelValue', 3)
    await flushPromises()
    await click(button('Save'))

    expect(createAccess).toHaveBeenCalledWith(expect.objectContaining({ accountId: 1, adminRoleId: 3 }))
    expect(toasts()[0]).toMatchObject({ color: 'success', description: 'Access added successfully!' })
    expect(onClose).toHaveBeenCalledWith(false)
  })

  it("doesn't reload accounts that are already loaded", async () => {
    const accountsStore = useAdminAccountsStore()
    accountsStore.accounts = [adminAccount(1)]
    const loadAll = vi.spyOn(accountsStore, 'loadAllAccounts')

    await openDialog(AdminAccessSettingsDialog, { editingAdmin: { accountId: 1, adminRoleId: 1 } })

    expect(loadAll).not.toHaveBeenCalled()
    expect(bodyText()).toContain('Edit Admin Access')
  })

  it("shows an error when accounts can't be loaded", async () => {
    vi.spyOn(useAdminAccountsStore(), 'loadAllAccounts').mockRejectedValue(new Error('down'))

    await openDialog(AdminAccessSettingsDialog, { editingAdmin: {} })

    expect(toasts()[0]).toMatchObject({ color: 'error', description: 'Failed to load accounts. Please try again.' })
  })

  it('closes without saving from Cancel', async () => {
    useAdminAccountsStore().accounts = [adminAccount(1)]
    const { onClose } = await openDialog(AdminAccessSettingsDialog, { editingAdmin: {} })

    await click(button('Cancel'))

    expect(onClose).toHaveBeenCalledWith(false)
  })

  // createAdminAccess reports failure by returning false rather than throwing, but the dialog only checks
  // for a throw, so a failed grant still says "Access added successfully!" and closes
  it.skip('BUG: reports a failed grant', async () => {
    useAdminAccountsStore().accounts = [adminAccount(1)]
    vi.spyOn(useAdminAccessStore(), 'createAdminAccess').mockResolvedValue(false)
    const { onClose } = await openDialog(AdminAccessSettingsDialog, { editingAdmin: { accountId: 1, adminRoleId: 1 } })

    await click(button('Save'))

    expect(toasts()[0]).toMatchObject({ color: 'error' })
    expect(onClose).not.toHaveBeenCalled()
  })
})

describe('AdminTeamEventDialog', () => {
  it('saves a team event', async () => {
    const { wrapper, onClose } = await openDialog(AdminTeamEventDialog, { editingTeamEvent: {} })

    expect(bodyText()).toContain('Add Team Event')
    wrapper.findAllComponents(USelect)[0].vm.$emit('update:modelValue', 'Aqua')
    wrapper.findAllComponents(USelect)[1].vm.$emit('update:modelValue', '+1 Aid Slot')
    await flushPromises()
    await click(button('Save'))

    expect(toasts()[0]).toMatchObject({ color: 'success', description: 'Team event saved successfully!' })
    expect(onClose).toHaveBeenCalledWith(false)
  })

  it('edits an existing team event', async () => {
    const { onClose } = await openDialog(AdminTeamEventDialog, { editingTeamEvent: { id: 1, team: 'Red', eventType: '-1 Aid Slot', eventName: 'Penalty' } })

    expect(bodyText()).toContain('Edit Team Event')
    await click(button('Cancel'))

    expect(onClose).toHaveBeenCalledWith(false)
  })
})

describe('AidActivityFilter', () => {
  it('applies search text, direction and a date range', async () => {
    const store = useAdminAidActivityStore()
    const { wrapper, onClose } = await openDialog(AidActivityFilter, { filters: {} })
    const direction = () => wrapper.findComponent(USelect)

    expect(direction().props('disabled')).toBe(true)
    await type(inputByPlaceholder('Enter name to search...'), '  Bob  ')
    expect(direction().props('disabled')).toBe(false)
    direction().vm.$emit('update:modelValue', 'received')
    await click(button('Pick a date'))
    wrapper.findComponent(UCalendar).vm.$emit('update:modelValue', { start: new CalendarDate(2026, 10, 1), end: new CalendarDate(2026, 10, 9) })
    await flushPromises()
    expect(bodyText()).toContain('Oct 1, 2026 - Oct 9, 2026')
    await click(button('Apply Filters'))

    expect(store.filters).toEqual({ search: 'Bob', direction: 'received', sentSince: new Date(2026, 9, 1), sentUntil: new Date(2026, 9, 9) })
    expect(onClose).toHaveBeenCalled()
  })

  it('searches a single day when only a start date is picked', async () => {
    const store = useAdminAidActivityStore()
    const { wrapper } = await openDialog(AidActivityFilter, { filters: {} })

    await click(button('Pick a date'))
    wrapper.findComponent(UCalendar).vm.$emit('update:modelValue', { start: new CalendarDate(2026, 10, 1), end: undefined })
    await flushPromises()
    expect(bodyText()).toContain('Oct 1, 2026')
    await click(button('Apply Filters'))

    expect(store.filters).toEqual({ search: undefined, direction: undefined, sentSince: new Date(2026, 9, 1), sentUntil: new Date(2026, 9, 1) })
  })

  it('starts from the current filters, and clears them', async () => {
    const store = useAdminAidActivityStore()
    const { wrapper } = await openDialog(AidActivityFilter, {
      filters: { search: 'Sue', direction: 'sent', sentSince: new Date(2026, 9, 1), sentUntil: new Date(2026, 9, 3) },
    })

    expect(inputByPlaceholder('Enter name to search...').value).toBe('Sue')
    expect(wrapper.findComponent(USelect).props('modelValue')).toBe('sent')
    expect(bodyText()).toContain('Oct 1, 2026 - Oct 3, 2026')

    await click(button('Clear'))
    expect(bodyText()).toContain('Pick a date')
    await click(button('Apply Filters'))

    expect(store.filters).toEqual({ search: undefined, direction: undefined, sentSince: undefined, sentUntil: undefined })
  })
})
