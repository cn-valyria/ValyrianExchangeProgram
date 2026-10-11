import { describe, expect, it, vi } from 'vitest'
import { UDropdownMenu } from '#components'
import NavBar from '~/components/NavBar.vue'
import AccountRole from '~/components/chip/AccountRole.vue'
import AllianceAcronym from '~/components/chip/AllianceAcronym.vue'
import DefaultLayout from '~/layouts/default.vue'
import FullScreenLayout from '~/layouts/full-screen.vue'
import { click, mountInApp } from '../helpers/dom'

const signIn = (...adminRoles: string[]) => {
  const authStore = useAuthStore()
  authStore.isAuthenticated = true
  authStore.user = { accountId: 42, roles: adminRoles.map((name, id) => ({ id, name })) }
  return authStore
}

const linkTexts = (root: Element) => Array.from(root.querySelectorAll('a')).map((a) => a.textContent!.trim())

describe('NavBar', () => {
  const openMobileMenu = async (root: Element) => {
    // The menu toggle is the only icon-only button
    await click(Array.from(root.querySelectorAll('button')).find((b) => b.textContent!.trim() === '')!)
  }

  it('offers signed-out visitors a way to log in', async () => {
    const wrapper = await mountInApp(NavBar)
    await openMobileMenu(wrapper.element)

    expect(linkTexts(wrapper.element)).toEqual(expect.arrayContaining(['Home', 'How It Works', 'Log In']))
    expect(wrapper.findComponent(UDropdownMenu).exists()).toBe(false)
  })

  it.each([
    [[], ['My Account'], ['Admin Portal', 'Messages', 'Settings']],
    [['Admin - Lists'], ['My Account', 'Messages'], ['Admin Portal', 'Settings']],
    [['Admin (Read-only)'], ['My Account', 'Accounts', 'Aid Activity', 'Deals', 'Messages'], ['Settings']],
    [['Super Admin'], ['My Account', 'Accounts', 'Aid Activity', 'Deals', 'Messages', 'Settings'], []],
  ])('shows accounts with roles %j the pages they can use', async (roles, shown, hidden) => {
    signIn(...roles)
    const wrapper = await mountInApp(NavBar)

    const menuLabels = (wrapper.findComponent(UDropdownMenu).props('items') as { label: string }[][]).flat().map((item) => item.label)
    expect(menuLabels).toEqual(expect.arrayContaining([...shown, 'Log Out']))
    hidden.forEach((label) => expect(menuLabels).not.toContain(label))

    await openMobileMenu(wrapper.element)
    const mobileLinks = linkTexts(wrapper.element)
    expect(mobileLinks).toEqual(expect.arrayContaining([...shown, 'Log Out']))
    hidden.filter((label) => label !== 'Admin Portal').forEach((label) => expect(mobileLinks).not.toContain(label))
  })

  it("links to the signed-in account's page", async () => {
    signIn()
    const wrapper = await mountInApp(NavBar)

    const myAccount = (wrapper.findComponent(UDropdownMenu).props('items') as { label: string; to?: string }[][])[0][0]
    expect(myAccount).toMatchObject({ label: 'My Account', to: '/account/42' })
  })

  it('logs out from either menu', async () => {
    const authStore = signIn()
    const logout = vi.spyOn(authStore, 'logout').mockImplementation(() => {})
    const wrapper = await mountInApp(NavBar)

    const items = (wrapper.findComponent(UDropdownMenu).props('items') as { label: string; onSelect?: () => void }[][]).flat()
    items.find((item) => item.label === 'Log Out')!.onSelect!()
    await openMobileMenu(wrapper.element)
    await click(Array.from(wrapper.element.querySelectorAll('a')).find((a) => a.textContent!.trim() === 'Log Out')!)

    expect(logout).toHaveBeenCalledTimes(2)
  })
})

describe('AccountRole chip', () => {
  it.each([
    ['B', 'Buyer'],
    ['S', 'Seller'],
    ['C', 'Cash Collector'],
    ['H', 'On Hold'],
  ])('describes role %s', async (code, description) => {
    const wrapper = await mountInApp(AccountRole, { code })

    expect(wrapper.text()).toBe(`${code}${description}`)
  })

  it('shows nothing extra for an unknown role', async () => {
    const wrapper = await mountInApp(AccountRole, { code: 'Z' })

    expect(wrapper.text()).toBe('Z')
  })
})

describe('AllianceAcronym chip', () => {
  it('shows the acronym and logo of a known alliance', async () => {
    const wrapper = await mountInApp(AllianceAcronym, { allianceName: 'The Imperial Order' })

    expect(wrapper.text()).toBe('TIO')
    expect(wrapper.find('img').attributes('src')).toContain('alliance/tio.jpg')
  })

  it('uses a placeholder logo for other alliances', async () => {
    const wrapper = await mountInApp(AllianceAcronym, { allianceName: 'Valyrian Exchange Program' })

    expect(wrapper.text()).toBe('VEP')
    expect(wrapper.find('img').attributes('src')).toContain('alliance/none.jpg')
  })
})

describe('layouts', () => {
  it('the default layout includes the nav bar', async () => {
    const wrapper = await mountInApp(DefaultLayout)

    expect(wrapper.find('nav').exists()).toBe(true)
  })

  it('the full-screen layout leaves it out', async () => {
    const wrapper = await mountInApp(FullScreenLayout)

    expect(wrapper.find('nav').exists()).toBe(false)
  })
})
